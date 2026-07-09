using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public static class ViveGazeHelper
{
    private static float nextAllowedTime = 0f;

    public static bool TryGetGaze(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (Time.unscaledTime < nextAllowedTime)
        {
            return false;
        }

        try
        {
            if (XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes))
            {
                if (out_gazes != null && out_gazes.Length >= 2)
                {
                    var left = out_gazes[0];
                    var right = out_gazes[1];
                    if (left.isValid && right.isValid)
                    {
                        var lp = new Vector3(left.gazePose.position.x, left.gazePose.position.y, -left.gazePose.position.z);
                        var rp = new Vector3(right.gazePose.position.x, right.gazePose.position.y, -right.gazePose.position.z);
                        var lr = new Quaternion(-left.gazePose.orientation.x, -left.gazePose.orientation.y, left.gazePose.orientation.z, left.gazePose.orientation.w);
                        var rr = new Quaternion(-right.gazePose.orientation.x, -right.gazePose.orientation.y, right.gazePose.orientation.z, right.gazePose.orientation.w);
                        position = (lp + rp) * 0.5f;
                        Vector3 dir = ((lr * Vector3.forward) + (rr * Vector3.forward)).normalized;
                        rotation = Quaternion.LookRotation(dir);
                        return true;
                    }
                    else if (left.isValid)
                    {
                        position = new Vector3(left.gazePose.position.x, left.gazePose.position.y, -left.gazePose.position.z);
                        rotation = new Quaternion(-left.gazePose.orientation.x, -left.gazePose.orientation.y, left.gazePose.orientation.z, left.gazePose.orientation.w);
                        return true;
                    }
                    else if (right.isValid)
                    {
                        position = new Vector3(right.gazePose.position.x, right.gazePose.position.y, -right.gazePose.position.z);
                        rotation = new Quaternion(-right.gazePose.orientation.x, -right.gazePose.orientation.y, right.gazePose.orientation.z, right.gazePose.orientation.w);
                        return true;
                    }
                }
            }
        }
        catch {}

        // If we reach here, the HTC plugin failed (e.g. XR_ERROR_SESSION_LOST because the headset is off).
        // Apply a 2-second cooldown so it doesn't spam the console every frame.
        nextAllowedTime = Time.unscaledTime + 2.0f;
        return false;
    }
}
