using UnityEngine;

/// <summary>
/// Tilt Brush style Camera Controls
/// Code inspired by Unity's XR Editor
/// Replace OVRInput with your own Input
/// </summary>
public class TiltBrushCameraControls : MonoBehaviour
{
    private const float FAST_MOVE_SPEED = 20f;
    private const float SLOW_MOVE_SPEED = 1f;
    private const float MIN_SCALE = 1f;
    private const float MAX_SCALE = 100f;

    [Tooltip("Parent Transform of the Camera")]
    public Transform CameraRig;

    [Tooltip("Transform of the Camera")] 
    public Transform Camera;

    [Tooltip("Left Hand Transform")] 
    public Transform LeftHand;

    [Tooltip("Right Hand Transform")] 
    public Transform RightHand;

    private bool _isCrawling;
    private bool _allowScaling = true;
    private bool _isScaling;
    private float _startScale;
    private float _startDistance;
    private Vector3 _startPosition;
    private Vector3 _startMidPoint;
    private Vector3 _startDirection;
    private float _startYaw;
    private Vector3 _handPrevPos;
    private Transform _handPrev;

    private void Update()
    {
        TwoHandedScale();

        Flying();

        Crawl();
    }

    /// <summary>
    /// Moves the Camera Rig based on the gripped Hand's movement
    /// </summary>
    private void Crawl()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
        {
            _handPrevPos = LeftHand.position;
            _handPrev = LeftHand;
        }
        else if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            _handPrevPos = RightHand.position;
            _handPrev = RightHand;
        }

        bool isGripped = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch) || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        if (isGripped)
        {
            if (!_isCrawling)
            {
                _isCrawling = true;
                Vector3 crawlDirection = Vector3.zero;
                Vector3 crawlAmount = _handPrevPos - _handPrev.position;
                crawlDirection += crawlAmount;

                CameraRig.transform.position += crawlDirection;
            }

            // Press both triggers to reset to origin
            if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) && OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                _allowScaling = false;

                CameraRig.position = Vector3.zero;
                CameraRig.rotation = Quaternion.identity;

                // ResetViewerScale();
            }


            _handPrevPos = _handPrev.position;
        }

        _isCrawling = false;
    }

    /// <summary>
    /// Scales the Camera Rig based on the distance between the 2 hands and rotates the rig
    /// </summary>
    private void TwoHandedScale()
    {
        bool isGripped = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        if (isGripped)
        {
            if (_allowScaling)
            {
                bool otherGripHeld = false;
                bool isOtherGripped = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
                if (isOtherGripped)
                {
                    otherGripHeld = true;

                    Vector3 leftHandPosition = CameraRig.InverseTransformPoint(LeftHand.position);
                    Vector3 rightHandPosition = CameraRig.InverseTransformPoint(RightHand.position);
                    float distance = Vector3.Distance(leftHandPosition, rightHandPosition);

                    Vector3 handToHand = rightHandPosition - leftHandPosition;
                    Vector3 midPoint = leftHandPosition + handToHand * 0.5f;

                    handToHand.y = 0; // Use for yaw rotation

                    Quaternion pivotYaw = ConstrainYawRotation(CameraRig.rotation);

                    if (!_isScaling)
                    {
                        _startScale = GetViewerScale();
                        _startDistance = distance;
                        _startMidPoint = pivotYaw * midPoint * _startScale;
                        _startPosition = CameraRig.position;
                        _startDirection = handToHand;
                        _startYaw = CameraRig.rotation.eulerAngles.y;
                    }

                    _isScaling = true;
                    _isCrawling = false;

                    float currentScale = Mathf.Clamp(_startScale * (_startDistance / distance), MIN_SCALE, MAX_SCALE);

                    // Press both thumb buttons to reset scale
                    if (OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch) && OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch))
                    {
                        _allowScaling = false;

                        handToHand = RightHand.position - LeftHand.position;
                        midPoint = Camera.position + handToHand * 0.5f;
                        var currOffset = midPoint - CameraRig.position;

                        CameraRig.position = midPoint - currOffset / currentScale;
                        CameraRig.rotation = Quaternion.AngleAxis(_startYaw, Vector3.up);

                        ResetViewerScale();
                    }
                    
                    if (_allowScaling)
                    {
                        float yawSign = Mathf.Sign(Vector3.Dot(Quaternion.AngleAxis(90, Vector3.down) * _startDirection, handToHand));
                        float currentYaw = _startYaw + Vector3.Angle(_startDirection, handToHand) * yawSign;
                        Quaternion currentRotation = Quaternion.AngleAxis(currentYaw, Vector3.up);
                        midPoint = currentRotation * midPoint * currentScale;

                        Vector3 pos = _startPosition + _startMidPoint - midPoint;
                        CameraRig.position = pos;

                        CameraRig.rotation = currentRotation;

                        SetViewerScale(currentScale);
                    }
                }

                if (!otherGripHeld)
                    CancelScale();
            }
        }
        else
        {
            CancelScale();
        }
    }

    /// <summary>
    /// Moves the Camera Rig based on the HMD's looking direction
    /// </summary>
    private void Flying()
    {
        Vector2 thumbstickAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        float speed = SLOW_MOVE_SPEED;
        float speedControl = thumbstickAxis.y;

        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
            speed = FAST_MOVE_SPEED * speedControl;
        }
        else
        {
            speed = SLOW_MOVE_SPEED * speedControl;
        }

        speed *= GetViewerScale();
        CameraRig.Translate(Quaternion.Inverse(CameraRig.rotation) * Camera.transform.forward * (speed * Time.unscaledDeltaTime));
    }

    private void ResetViewerScale()
    {
        SetViewerScale(1f);
    }

    private void CancelScale()
    {
        _allowScaling = true;
        _isScaling = false;
    }

    
    private float GetViewerScale()
    {
        return CameraRig.localScale.x;
    }

    private void SetViewerScale(float scale)
    {
        CameraRig.localScale = new Vector3(scale, scale, scale);
    }

    /// <summary>
    /// Returns a rotation which only contains the yaw component of the given rotation
    /// </summary>
    /// <param name="rotation">The rotation we would like to constrain</param>
    /// <returns>A yaw-only rotation which matches the input's yaw</returns>
    private static Quaternion ConstrainYawRotation(Quaternion rotation)
    {
        rotation.x = 0;
        rotation.z = 0;
        return rotation;
    }
}