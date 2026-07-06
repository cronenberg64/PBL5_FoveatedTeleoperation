using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class PupilVerificationTest : MonoBehaviour
{
    private float startTime;
    private bool isTesting = true;

    void Start()
    {
        startTime = Time.time;
        Debug.Log("--- STARTING PUPIL VERIFICATION TEST ---");
    }

    void Update()
    {
        if (!isTesting) return;

        if (Time.time - startTime > 10f)
        {
            isTesting = false;
            Debug.Log("--- PUPIL VERIFICATION TEST FINISHED ---");
            return;
        }

        if (XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] pupilDataArray) && pupilDataArray != null && pupilDataArray.Length >= 2)
        {
            var leftPupil = pupilDataArray[0];
            var rightPupil = pupilDataArray[1];

            Debug.Log($"[Pupil] L(diamValid:{leftPupil.isDiameterValid}, diam:{leftPupil.pupilDiameter}, posValid:{leftPupil.isPositionValid}, pos:{leftPupil.pupilPosition.x:F3},{leftPupil.pupilPosition.y:F3}) | " +
                      $"R(diamValid:{rightPupil.isDiameterValid}, diam:{rightPupil.pupilDiameter}, posValid:{rightPupil.isPositionValid}, pos:{rightPupil.pupilPosition.x:F3},{rightPupil.pupilPosition.y:F3})");
        }
        else
        {
            Debug.LogWarning("[Pupil] GetEyePupilData returned false or null array!");
        }
    }
}
