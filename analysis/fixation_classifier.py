import csv
import math
import sys
import os

# I-VT Parameters
VELOCITY_THRESHOLD_DEG_PER_SEC = 30.0
MIN_FIXATION_DURATION_SEC = 0.100

# Dummy Target (Sphere for simple ray intersection)
# In Unity world space: e.g. floating slightly in front of the start point
TARGET_CENTER = (0.0, 1.5, 5.0) 
TARGET_RADIUS = 0.5

def dot_product(v1, v2):
    return v1[0]*v2[0] + v1[1]*v2[1] + v1[2]*v2[2]

def normalize(v):
    mag = math.sqrt(dot_product(v, v))
    if mag == 0: return (0,0,0)
    return (v[0]/mag, v[1]/mag, v[2]/mag)

def ray_intersects_sphere(origin, dir, center, radius):
    # Algebraic ray-sphere intersection
    # Ray: P(t) = origin + t*dir
    oc = (origin[0] - center[0], origin[1] - center[1], origin[2] - center[2])
    a = dot_product(dir, dir)
    b = 2.0 * dot_product(oc, dir)
    c = dot_product(oc, oc) - radius*radius
    discriminant = b*b - 4*a*c
    
    if discriminant > 0:
        t1 = (-b - math.sqrt(discriminant)) / (2.0*a)
        t2 = (-b + math.sqrt(discriminant)) / (2.0*a)
        if t1 > 0 or t2 > 0:
            return True
    return False

def analyze_gaze_telemetry(csv_path):
    if not os.path.exists(csv_path):
        print(f"Error: File not found {csv_path}")
        return

    with open(csv_path, 'r', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        rows = list(reader)

    if len(rows) < 2:
        print("Not enough rows for analysis.")
        return

    fixation_count = 0
    saccade_count = 0
    total_fixation_duration = 0.0
    
    # State tracking for I-VT
    is_in_fixation = False
    current_fixation_start_t = 0.0
    current_fixation_start_idx = 0
    
    # List of valid fixations: (start_t, end_t, avg_origin, avg_dir)
    fixations = []

    for i in range(1, len(rows)):
        prev = rows[i-1]
        curr = rows[i]
        
        t1 = float(prev['timestamp'])
        t2 = float(curr['timestamp'])
        dt = t2 - t1
        
        if dt <= 0: continue
        
        dir1 = (float(prev['gaze_dir_x']), float(prev['gaze_dir_y']), float(prev['gaze_dir_z']))
        dir2 = (float(curr['gaze_dir_x']), float(curr['gaze_dir_y']), float(curr['gaze_dir_z']))
        
        # Calculate angle between vectors
        d = max(-1.0, min(1.0, dot_product(dir1, dir2)))
        theta_rad = math.acos(d)
        theta_deg = math.degrees(theta_rad)
        
        velocity = theta_deg / dt
        
        if velocity < VELOCITY_THRESHOLD_DEG_PER_SEC:
            # Below threshold -> Fixation candidate
            if not is_in_fixation:
                is_in_fixation = True
                current_fixation_start_t = t1
                current_fixation_start_idx = i - 1
        else:
            # Above threshold -> Saccade / end of fixation
            if is_in_fixation:
                is_in_fixation = False
                duration = t1 - current_fixation_start_t
                if duration >= MIN_FIXATION_DURATION_SEC:
                    # Valid fixation
                    fixation_count += 1
                    total_fixation_duration += duration
                    
                    # Compute average origin and dir for this fixation
                    sum_ox, sum_oy, sum_oz = 0.0, 0.0, 0.0
                    sum_dx, sum_dy, sum_dz = 0.0, 0.0, 0.0
                    n = i - current_fixation_start_idx
                    for j in range(current_fixation_start_idx, i):
                        sum_ox += float(rows[j]['gaze_origin_x'])
                        sum_oy += float(rows[j]['gaze_origin_y'])
                        sum_oz += float(rows[j]['gaze_origin_z'])
                        sum_dx += float(rows[j]['gaze_dir_x'])
                        sum_dy += float(rows[j]['gaze_dir_y'])
                        sum_dz += float(rows[j]['gaze_dir_z'])
                        
                    avg_o = (sum_ox/n, sum_oy/n, sum_oz/n)
                    avg_d = normalize((sum_dx/n, sum_dy/n, sum_dz/n))
                    
                    fixations.append((current_fixation_start_t, t1, avg_o, avg_d))
                else:
                    # Too short, count as part of saccade/noise
                    pass
                saccade_count += 1

    # Catch final fixation if it ends at EOF
    if is_in_fixation:
        duration = float(rows[-1]['timestamp']) - current_fixation_start_t
        if duration >= MIN_FIXATION_DURATION_SEC:
            fixation_count += 1
            total_fixation_duration += duration
            
            sum_ox, sum_oy, sum_oz = 0.0, 0.0, 0.0
            sum_dx, sum_dy, sum_dz = 0.0, 0.0, 0.0
            n = len(rows) - current_fixation_start_idx
            for j in range(current_fixation_start_idx, len(rows)):
                sum_ox += float(rows[j]['gaze_origin_x'])
                sum_oy += float(rows[j]['gaze_origin_y'])
                sum_oz += float(rows[j]['gaze_origin_z'])
                sum_dx += float(rows[j]['gaze_dir_x'])
                sum_dy += float(rows[j]['gaze_dir_y'])
                sum_dz += float(rows[j]['gaze_dir_z'])
            avg_o = (sum_ox/n, sum_oy/n, sum_oz/n)
            avg_d = normalize((sum_dx/n, sum_dy/n, sum_dz/n))
            fixations.append((current_fixation_start_t, float(rows[-1]['timestamp']), avg_o, avg_d))

    mean_fixation_dur = (total_fixation_duration / fixation_count) if fixation_count > 0 else 0.0

    print(f"--- I-VT Fixation Analysis ---")
    print(f"Total Rows Analyzed: {len(rows)}")
    print(f"Total Duration: {float(rows[-1]['timestamp']) - float(rows[0]['timestamp']):.2f} sec")
    print(f"Fixation Count: {fixation_count}")
    print(f"Saccade Count:  {saccade_count}")
    print(f"Mean Fixation Duration: {mean_fixation_dur:.3f} sec")
    
    # Target intersection check
    first_fixation_time = -1
    for fix in fixations:
        start_t, end_t, avg_o, avg_d = fix
        if ray_intersects_sphere(avg_o, avg_d, TARGET_CENTER, TARGET_RADIUS):
            # Time from start of trial to the START of the first on-target fixation
            first_fixation_time = start_t - float(rows[0]['timestamp'])
            print(f"-> TARGET FOUND! First fixation on target at {first_fixation_time:.3f} seconds.")
            break
            
    if first_fixation_time < 0:
        print("-> Target was NOT fixated during this trial (based on current dummy target coords).")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python fixation_classifier.py <path_to_gaze_telemetry.csv>")
        sys.exit(1)
    
    analyze_gaze_telemetry(sys.argv[1])
