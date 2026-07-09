using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class GazeTelemetryLogger : MonoBehaviour
{
    public int logIntervalFrames = 1;
    
    private bool isLogging = false;
    private StreamWriter csvWriter;
    private int frameCounter = 0;
    private StringBuilder writeBuffer = new StringBuilder(4096);
    
    // Blink detection state
    private float lastLeftOpenness = 1f;
    private float lastRightOpenness = 1f;
    private int leftBlinkCount = 0;
    private int rightBlinkCount = 0;
    private float logStartTime = 0f;

    private const float BLINK_THRESHOLD = 0.2f;

    public void StartLogging(string filepath)
    {
        try
        {
            csvWriter = new StreamWriter(new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, true), Encoding.UTF8);
            csvWriter.WriteLine("timestamp,gaze_origin_x,gaze_origin_y,gaze_origin_z,gaze_dir_x,gaze_dir_y,gaze_dir_z,left_eye_open,right_eye_open,head_pos_x,head_pos_y,head_pos_z,head_rot_x,head_rot_y,head_rot_z,pupil_diameter_left,pupil_diameter_right,pupil_valid_left,pupil_valid_right");
            
            isLogging = true;
            frameCounter = 0;
            leftBlinkCount = 0;
            rightBlinkCount = 0;
            lastLeftOpenness = 1f;
            lastRightOpenness = 1f;
            logStartTime = Time.time;
            
            Debug.Log($"[GazeTelemetryLogger] Started logging to {filepath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GazeTelemetryLogger] Failed to open CSV: {e.Message}");
            isLogging = false;
        }
    }

    public void StopLogging(out float blinkRatePerMin)
    {
        isLogging = false;
        blinkRatePerMin = 0f;

        if (csvWriter != null)
        {
            float durationMin = (Time.time - logStartTime) / 60f;
            if (durationMin > 0)
            {
                blinkRatePerMin = ((leftBlinkCount + rightBlinkCount) / 2f) / durationMin;
            }

            if (writeBuffer.Length > 0)
            {
                csvWriter.Write(writeBuffer.ToString());
                writeBuffer.Clear();
            }

            csvWriter.Flush();
            csvWriter.Close();
            csvWriter = null;
            
            Debug.Log($"[GazeTelemetryLogger] Stopped logging. Blinks: L={leftBlinkCount}, R={rightBlinkCount}, Rate={blinkRatePerMin:F2}/min");
        }
    }

    private void Update()
    {
        if (!isLogging) return;

        frameCounter++;
        if (frameCounter % logIntervalFrames != 0) return;

        float timestamp = Time.time;
        
        // 1. Head Pose
        Vector3 headPos = Vector3.zero;
        Vector3 headRot = Vector3.zero;
        if (Camera.main != null)
        {
            headPos = Camera.main.transform.position;
            headRot = Camera.main.transform.eulerAngles;
        }

        // 2. Gaze Origin / Direction
        Vector3 gazeOrigin = Vector3.zero;
        Vector3 gazeDir = Vector3.forward;
        if (ViveGazeHelper.TryGetGaze(out Vector3 origin, out Quaternion rotation))
        {
            gazeOrigin = origin;
            gazeDir = rotation * Vector3.forward;
        }

        // 3. Eye Openness
        float leftOpen = 1f;
        float rightOpen = 1f;
        try
        {
            if (XR_HTC_eye_tracker.Interop.GetEyeGeometricData(out XrSingleEyeGeometricDataHTC[] geometricData) && geometricData != null && geometricData.Length >= 2)
            {
                if (geometricData[0].isValid) leftOpen = geometricData[0].eyeOpenness;
                if (geometricData[1].isValid) rightOpen = geometricData[1].eyeOpenness;
            }
        }
        catch (Exception)
        {
            // Ignore exception if eye tracking is unavailable
        }

        // Blink detection (transition from > 0.2 to <= 0.2)
        if (lastLeftOpenness > BLINK_THRESHOLD && leftOpen <= BLINK_THRESHOLD) leftBlinkCount++;
        if (lastRightOpenness > BLINK_THRESHOLD && rightOpen <= BLINK_THRESHOLD) rightBlinkCount++;
        lastLeftOpenness = leftOpen;
        lastRightOpenness = rightOpen;

        // 4. Pupil Data
        float pupilDiamLeft = 0f;
        float pupilDiamRight = 0f;
        bool pupilValidLeft = false;
        bool pupilValidRight = false;

        try
        {
            if (XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] pupilData) && pupilData != null && pupilData.Length >= 2)
            {
                var l = pupilData[0];
                var r = pupilData[1];
                
                pupilValidLeft = l.isDiameterValid;
                if (pupilValidLeft) pupilDiamLeft = l.pupilDiameter;
                
                pupilValidRight = r.isDiameterValid;
                if (pupilValidRight) pupilDiamRight = r.pupilDiameter;
            }
        }
        catch (Exception)
        {
            // Ignore exception if pupil data is unavailable
        }

        // Write row to buffer
        if (csvWriter != null)
        {
            writeBuffer.AppendLine($"{timestamp:F3}," +
                              $"{gazeOrigin.x:F4},{gazeOrigin.y:F4},{gazeOrigin.z:F4}," +
                              $"{gazeDir.x:F4},{gazeDir.y:F4},{gazeDir.z:F4}," +
                              $"{leftOpen:F3},{rightOpen:F3}," +
                              $"{headPos.x:F4},{headPos.y:F4},{headPos.z:F4}," +
                              $"{headRot.x:F3},{headRot.y:F3},{headRot.z:F3}," +
                              $"{pupilDiamLeft:F3},{pupilDiamRight:F3},{(pupilValidLeft?1:0)},{(pupilValidRight?1:0)}");

            if (writeBuffer.Length > 4000)
            {
                string data = writeBuffer.ToString();
                writeBuffer.Clear();
                csvWriter.Write(data);
            }
        }
    }

    private void OnDestroy()
    {
        if (isLogging)
        {
            StopLogging(out _);
        }
    }
}
