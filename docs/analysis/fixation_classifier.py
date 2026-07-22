import csv
import math
import sys
import os

# I-VT Parameters
VELOCITY_THRESHOLD_DEG_PER_SEC = 30.0
MIN_FIXATION_DURATION_SEC = 0.100
SMOOTHING_WINDOW_SIZE = 5

# Pre-filter Parameters
IMPLAUSIBLE_VELOCITY_DEG_PER_SEC = 400.0

# Dummy Target (Sphere for simple ray intersection)
TARGET_CENTER = (0.0, 1.5, 5.0) 
TARGET_RADIUS = 0.5

def dot_product(v1, v2):
    return v1[0]*v2[0] + v1[1]*v2[1] + v1[2]*v2[2]

def normalize(v):
    mag = math.sqrt(dot_product(v, v))
    if mag == 0: return (0,0,0)
    return (v[0]/mag, v[1]/mag, v[2]/mag)

def angle_between(dir1, dir2):
    d = max(-1.0, min(1.0, dot_product(dir1, dir2)))
    return math.degrees(math.acos(d))

def ray_intersects_sphere(origin, dir, center, radius):
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

    if len(rows) < SMOOTHING_WINDOW_SIZE * 2:
        print("Not enough rows for analysis.")
        return

    # --- 1. Correlation Analysis ---
    high_vel_frames = 0
    invalid_pupil_in_high_vel = 0
    
    for i in range(1, len(rows)):
        t1 = float(rows[i-1]['timestamp'])
        t2 = float(rows[i]['timestamp'])
        dt = t2 - t1
        if dt <= 0: continue
        
        dir1 = (float(rows[i-1]['gaze_dir_x']), float(rows[i-1]['gaze_dir_y']), float(rows[i-1]['gaze_dir_z']))
        dir2 = (float(rows[i]['gaze_dir_x']), float(rows[i]['gaze_dir_y']), float(rows[i]['gaze_dir_z']))
        
        vel = angle_between(dir1, dir2) / dt
        if vel > IMPLAUSIBLE_VELOCITY_DEG_PER_SEC:
            high_vel_frames += 1
            # Check pupil valid in the same frame
            p_left = int(rows[i].get('pupil_valid_left', 1))
            p_right = int(rows[i].get('pupil_valid_right', 1))
            if p_left == 0 and p_right == 0:
                invalid_pupil_in_high_vel += 1

    print("--- Pre-Filter Correlation Analysis ---")
    print(f"Total raw single-frame spikes > {IMPLAUSIBLE_VELOCITY_DEG_PER_SEC} deg/s: {high_vel_frames}")
    if high_vel_frames > 0:
        pct = (invalid_pupil_in_high_vel / high_vel_frames) * 100
        print(f"Spikes with pupil_valid=false for BOTH eyes: {invalid_pupil_in_high_vel} ({pct:.1f}%)")
    print("-" * 40)

    # --- 2. Pre-Filtering ---
    # Flag rows to discard
    valid_flags = [True] * len(rows)
    for i in range(len(rows)):
        p_left = int(rows[i].get('pupil_valid_left', 1))
        p_right = int(rows[i].get('pupil_valid_right', 1))
        if p_left == 0 and p_right == 0:
            valid_flags[i] = False
            
    # Check for implausible jumps that return to baseline
    for i in range(1, len(rows)-1):
        if not valid_flags[i]: continue # already discarded
        
        t1 = float(rows[i-1]['timestamp'])
        t2 = float(rows[i]['timestamp'])
        dt = t2 - t1
        if dt <= 0: continue
        
        dir1 = (float(rows[i-1]['gaze_dir_x']), float(rows[i-1]['gaze_dir_y']), float(rows[i-1]['gaze_dir_z']))
        dir2 = (float(rows[i]['gaze_dir_x']), float(rows[i]['gaze_dir_y']), float(rows[i]['gaze_dir_z']))
        
        vel = angle_between(dir1, dir2) / dt
        
        if vel > IMPLAUSIBLE_VELOCITY_DEG_PER_SEC:
            # Check if it returns to baseline within 2 frames (check i+1 and i+2)
            returns = False
            for k in range(1, 3):
                if i+k < len(rows):
                    dir_future = (float(rows[i+k]['gaze_dir_x']), float(rows[i+k]['gaze_dir_y']), float(rows[i+k]['gaze_dir_z']))
                    angle_to_baseline = angle_between(dir1, dir_future)
                    # If the angle to the original baseline is small (e.g. less than half the jump)
                    if angle_to_baseline < (vel * dt) * 0.5:
                        returns = True
                        break
            if returns:
                valid_flags[i] = False

    # Interpolate discarded samples
    filtered_rows = []
    for i in range(len(rows)):
        if valid_flags[i]:
            filtered_rows.append(rows[i].copy())
        else:
            # Find previous valid and next valid
            prev_valid_idx = i - 1
            while prev_valid_idx >= 0 and not valid_flags[prev_valid_idx]:
                prev_valid_idx -= 1
                
            next_valid_idx = i + 1
            while next_valid_idx < len(rows) and not valid_flags[next_valid_idx]:
                next_valid_idx += 1
                
            new_row = rows[i].copy()
            if prev_valid_idx >= 0 and next_valid_idx < len(rows):
                # Interpolate
                t_prev = float(rows[prev_valid_idx]['timestamp'])
                t_next = float(rows[next_valid_idx]['timestamp'])
                t_curr = float(rows[i]['timestamp'])
                if t_next > t_prev:
                    f = (t_curr - t_prev) / (t_next - t_prev)
                    
                    dx_prev = float(rows[prev_valid_idx]['gaze_dir_x'])
                    dy_prev = float(rows[prev_valid_idx]['gaze_dir_y'])
                    dz_prev = float(rows[prev_valid_idx]['gaze_dir_z'])
                    
                    dx_next = float(rows[next_valid_idx]['gaze_dir_x'])
                    dy_next = float(rows[next_valid_idx]['gaze_dir_y'])
                    dz_next = float(rows[next_valid_idx]['gaze_dir_z'])
                    
                    interp_dir = normalize((dx_prev + f*(dx_next - dx_prev),
                                          dy_prev + f*(dy_next - dy_prev),
                                          dz_prev + f*(dz_next - dz_prev)))
                    new_row['gaze_dir_x'] = interp_dir[0]
                    new_row['gaze_dir_y'] = interp_dir[1]
                    new_row['gaze_dir_z'] = interp_dir[2]
            elif prev_valid_idx >= 0:
                new_row['gaze_dir_x'] = rows[prev_valid_idx]['gaze_dir_x']
                new_row['gaze_dir_y'] = rows[prev_valid_idx]['gaze_dir_y']
                new_row['gaze_dir_z'] = rows[prev_valid_idx]['gaze_dir_z']
            elif next_valid_idx < len(rows):
                new_row['gaze_dir_x'] = rows[next_valid_idx]['gaze_dir_x']
                new_row['gaze_dir_y'] = rows[next_valid_idx]['gaze_dir_y']
                new_row['gaze_dir_z'] = rows[next_valid_idx]['gaze_dir_z']
            
            filtered_rows.append(new_row)

    # --- 3. Moving Average Smoothing on Filtered Data ---
    smoothed_rows = []
    for i in range(len(filtered_rows)):
        start_idx = max(0, i - SMOOTHING_WINDOW_SIZE // 2)
        end_idx = min(len(filtered_rows), i + SMOOTHING_WINDOW_SIZE // 2 + 1)
        
        sum_dx, sum_dy, sum_dz = 0.0, 0.0, 0.0
        for j in range(start_idx, end_idx):
            sum_dx += float(filtered_rows[j]['gaze_dir_x'])
            sum_dy += float(filtered_rows[j]['gaze_dir_y'])
            sum_dz += float(filtered_rows[j]['gaze_dir_z'])
            
        n = end_idx - start_idx
        smoothed_dir = normalize((sum_dx/n, sum_dy/n, sum_dz/n))
        
        new_row = filtered_rows[i].copy()
        new_row['smoothed_gaze_dir_x'] = smoothed_dir[0]
        new_row['smoothed_gaze_dir_y'] = smoothed_dir[1]
        new_row['smoothed_gaze_dir_z'] = smoothed_dir[2]
        smoothed_rows.append(new_row)

    # --- 4. I-VT Classification ---
    fixation_count = 0
    saccade_count = 0
    total_fixation_duration = 0.0
    
    is_in_fixation = False
    current_fixation_start_t = 0.0
    current_fixation_start_idx = 0
    fixations = []

    for i in range(1, len(smoothed_rows)):
        prev = smoothed_rows[i-1]
        curr = smoothed_rows[i]
        
        t1 = float(prev['timestamp'])
        t2 = float(curr['timestamp'])
        dt = t2 - t1
        if dt <= 0: continue
        
        dir1 = (float(prev['smoothed_gaze_dir_x']), float(prev['smoothed_gaze_dir_y']), float(prev['smoothed_gaze_dir_z']))
        dir2 = (float(curr['smoothed_gaze_dir_x']), float(curr['smoothed_gaze_dir_y']), float(curr['smoothed_gaze_dir_z']))
        
        velocity = angle_between(dir1, dir2) / dt
        
        if velocity < VELOCITY_THRESHOLD_DEG_PER_SEC:
            if not is_in_fixation:
                is_in_fixation = True
                current_fixation_start_t = t1
                current_fixation_start_idx = i - 1
        else:
            if is_in_fixation:
                is_in_fixation = False
                duration = t1 - current_fixation_start_t
                if duration >= MIN_FIXATION_DURATION_SEC:
                    fixation_count += 1
                    total_fixation_duration += duration
                    
                    sum_ox, sum_oy, sum_oz = 0.0, 0.0, 0.0
                    sum_dx, sum_dy, sum_dz = 0.0, 0.0, 0.0
                    n = i - current_fixation_start_idx
                    for j in range(current_fixation_start_idx, i):
                        sum_ox += float(filtered_rows[j]['gaze_origin_x'])
                        sum_oy += float(filtered_rows[j]['gaze_origin_y'])
                        sum_oz += float(filtered_rows[j]['gaze_origin_z'])
                        sum_dx += float(filtered_rows[j]['gaze_dir_x'])
                        sum_dy += float(filtered_rows[j]['gaze_dir_y'])
                        sum_dz += float(filtered_rows[j]['gaze_dir_z'])
                        
                    avg_o = (sum_ox/n, sum_oy/n, sum_oz/n)
                    avg_d = normalize((sum_dx/n, sum_dy/n, sum_dz/n))
                    fixations.append((current_fixation_start_t, t1, avg_o, avg_d))
                saccade_count += 1

    if is_in_fixation:
        duration = float(filtered_rows[-1]['timestamp']) - current_fixation_start_t
        if duration >= MIN_FIXATION_DURATION_SEC:
            fixation_count += 1
            total_fixation_duration += duration
            
            sum_ox, sum_oy, sum_oz = 0.0, 0.0, 0.0
            sum_dx, sum_dy, sum_dz = 0.0, 0.0, 0.0
            n = len(filtered_rows) - current_fixation_start_idx
            for j in range(current_fixation_start_idx, len(filtered_rows)):
                sum_ox += float(filtered_rows[j]['gaze_origin_x'])
                sum_oy += float(filtered_rows[j]['gaze_origin_y'])
                sum_oz += float(filtered_rows[j]['gaze_origin_z'])
                sum_dx += float(filtered_rows[j]['gaze_dir_x'])
                sum_dy += float(filtered_rows[j]['gaze_dir_y'])
                sum_dz += float(filtered_rows[j]['gaze_dir_z'])
            avg_o = (sum_ox/n, sum_oy/n, sum_oz/n)
            avg_d = normalize((sum_dx/n, sum_dy/n, sum_dz/n))
            fixations.append((current_fixation_start_t, float(filtered_rows[-1]['timestamp']), avg_o, avg_d))

    mean_fixation_dur = (total_fixation_duration / fixation_count) if fixation_count > 0 else 0.0

    print(f"--- I-VT Fixation Analysis ---")
    print(f"Total Rows Analyzed: {len(rows)}")
    print(f"Total Duration: {float(rows[-1]['timestamp']) - float(rows[0]['timestamp']):.2f} sec")
    print(f"Fixation Count: {fixation_count}")
    print(f"Saccade Count:  {saccade_count}")
    print(f"Mean Fixation Duration: {mean_fixation_dur:.3f} sec")
    
    first_fixation_time = -1
    for fix in fixations:
        start_t, end_t, avg_o, avg_d = fix
        if ray_intersects_sphere(avg_o, avg_d, TARGET_CENTER, TARGET_RADIUS):
            first_fixation_time = start_t - float(rows[0]['timestamp'])
            print(f"-> TARGET FOUND! First fixation on target at {first_fixation_time:.3f} seconds.")
            break
            
    if first_fixation_time < 0:
        print("-> Target was NOT fixated during this trial (based on current dummy target coords).")

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print('Usage: python fixation_classifier.py <path_to_gaze_telemetry.csv>')
        sys.exit(1)
    
    analyze_gaze_telemetry(sys.argv[1])
