using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right mouse <b>click</b> toggles bow aim (Animator bool isAiming): on = <b>zoomed-in third person</b> beside the head
/// (over-the-shoulder: head + hands in frame, not full-body distant TPP, not FPP). Off = restored normal TPP defaults.
/// Runs before <see cref="PlayerCombat"/>.
/// Requires Animator parameter <c>isAiming</c> and Aim Overdraw state (see StarterAssetsThirdPerson controller).
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerBowAim : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private CinemachineVirtualCamera playerFollowCamera;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Input")]
    [Tooltip("0 = LMB, 1 = RMB")]
    [SerializeField] private int aimMouseButton = 1;
    [Tooltip("If true, each RMB click toggles aim on/off. If false, hold RMB to aim.")]
    [SerializeField] private bool toggleAimOnClick = true;

    [Header("Camera — bow aim (close OTS: head + hands, still TPP)")]
    [Tooltip("Follow distance while aiming: lower = zoom in closer to the head (still third-person, not FPP).")]
    [SerializeField] private float aimCameraDistance = 2.25f;
    [Tooltip("0 = left shoulder, 1 = right. ~0.8 = beside head / over-the-shoulder.")]
    [SerializeField] private float aimCameraSide = 0.82f;
    [SerializeField] private float cameraBlendSpeed = 14f;
    [SerializeField] private bool adjustFieldOfView = true;
    [Tooltip("Match or slightly narrow vs default TPP for a subtle zoom; main zoom is Camera Distance.")]
    [SerializeField] private float aimFieldOfView = 40f;
    [Tooltip("Shoulder offset scale: enough lateral offset to keep head & bow hand in frame (OTS).")]
    [SerializeField] private float aimShoulderOffsetScale = 0.42f;
    [Tooltip("Pivot height toward head/upper chest (Cinemachine Vertical Arm Length).")]
    [SerializeField] private float aimVerticalArmLength = 0.38f;

    [Header("Body vs look")]
    [Tooltip("ThirdPersonController only rotates the body when you move; while aiming we match camera yaw.")]
    [SerializeField] private bool alignBodyYawWithCameraWhileAiming = true;
    [SerializeField] private float bodyYawAlignSmoothTime = 0.06f;

    [Header("Movement")]
    [SerializeField] [Range(0.05f, 1f)] private float moveSpeedMultiplierWhileAiming = 0.35f;

    [Header("Shoot point (screen center)")]
    [SerializeField] private bool showShootPointWhileAiming = true;
    [SerializeField] private Color shootPointColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private float shootPointDotSize = 5f;
    [SerializeField] private float shootPointCrossLength = 10f;
    [SerializeField] private float shootPointCrossThickness = 1.5f;
    [SerializeField] private int crosshairCanvasSortOrder = 400;

    private Cinemachine3rdPersonFollow thirdPersonFollow;
    private float defaultCameraDistance;
    private float defaultCameraSide;
    private Vector3 defaultShoulderOffset;
    private float defaultVerticalArmLength;
    private float defaultFieldOfView;
    private bool cameraDefaultsCaptured;

    private float bodyYawVelocity;

    private float[] defaultLayerWeights;
    private float cachedMoveSpeed;
    private float cachedSprintSpeed;
    private bool moveSpeedsCaptured;

    private static readonly int IsAimingHash = Animator.StringToHash("isAiming");
    private static readonly int PunchKickHash = Animator.StringToHash("punchKick");

    public bool IsAiming => aiming;
    private bool aiming;

    private Canvas crosshairCanvas;
    private static Sprite s_pixelSprite;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (thirdPersonController == null)
            thirdPersonController = GetComponent<ThirdPersonController>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerFollowCamera == null)
            playerFollowCamera = FindFirstObjectByType<CinemachineVirtualCamera>();

        if (playerFollowCamera != null)
        {
            thirdPersonFollow = playerFollowCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>(true);
            defaultFieldOfView = playerFollowCamera.m_Lens.FieldOfView;
        }

        if (thirdPersonFollow != null)
        {
            defaultCameraDistance = thirdPersonFollow.CameraDistance;
            defaultCameraSide = thirdPersonFollow.CameraSide;
            defaultShoulderOffset = thirdPersonFollow.ShoulderOffset;
            defaultVerticalArmLength = thirdPersonFollow.VerticalArmLength;
            cameraDefaultsCaptured = true;
        }

        if (animator != null && animator.layerCount > 0)
        {
            defaultLayerWeights = new float[animator.layerCount];
            for (int i = 0; i < animator.layerCount; i++)
                defaultLayerWeights[i] = animator.GetLayerWeight(i);
        }

        if (showShootPointWhileAiming)
            EnsureCrosshairBuilt();
    }

    private void Start()
    {
        ResolveCameraRigIfNeeded();
    }

    private void OnEnable()
    {
        ResolveCameraRigIfNeeded();
    }

    /// <summary>VCam can be inactive in Awake order; resolve again once scene is fully loaded.</summary>
    private void ResolveCameraRigIfNeeded()
    {
        if (playerFollowCamera == null)
            playerFollowCamera = FindFirstObjectByType<CinemachineVirtualCamera>();
        if (playerFollowCamera == null) return;

        if (thirdPersonFollow == null)
            thirdPersonFollow = playerFollowCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>(true);

        if (thirdPersonFollow != null && !cameraDefaultsCaptured)
        {
            defaultCameraDistance = thirdPersonFollow.CameraDistance;
            defaultCameraSide = thirdPersonFollow.CameraSide;
            defaultShoulderOffset = thirdPersonFollow.ShoulderOffset;
            defaultVerticalArmLength = thirdPersonFollow.VerticalArmLength;
            if (playerFollowCamera != null)
                defaultFieldOfView = playerFollowCamera.m_Lens.FieldOfView;
            cameraDefaultsCaptured = true;
        }
    }

    private void Update()
    {
        if (DialogueManager.IsBlockingGameplay) return;
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (aiming)
                SetAiming(false);
            return;
        }

        if (toggleAimOnClick)
        {
            if (Input.GetMouseButtonDown(aimMouseButton))
                SetAiming(!aiming);
        }
        else
        {
            bool wantAim = Input.GetMouseButton(aimMouseButton);
            if (wantAim != aiming)
                SetAiming(wantAim);
        }
    }

    private void SetAiming(bool on)
    {
        ResolveCameraRigIfNeeded();

        aiming = on;

        if (animator != null && HasParameter(IsAimingHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(IsAimingHash, on);

        if (on && animator != null && HasParameter(PunchKickHash, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(PunchKickHash);

        ApplyLayerWeightsForAim(on);

        if (thirdPersonController != null)
        {
            if (!moveSpeedsCaptured)
            {
                cachedMoveSpeed = thirdPersonController.MoveSpeed;
                cachedSprintSpeed = thirdPersonController.SprintSpeed;
                moveSpeedsCaptured = true;
            }

            if (on)
            {
                thirdPersonController.MoveSpeed = cachedMoveSpeed * moveSpeedMultiplierWhileAiming;
                thirdPersonController.SprintSpeed = cachedSprintSpeed * moveSpeedMultiplierWhileAiming;
            }
            else
            {
                thirdPersonController.MoveSpeed = cachedMoveSpeed;
                thirdPersonController.SprintSpeed = cachedSprintSpeed;
            }
        }

        if (crosshairCanvas != null)
            crosshairCanvas.gameObject.SetActive(on && showShootPointWhileAiming);

        SnapCameraToCurrentMode();
    }

    private void SnapCameraToCurrentMode()
    {
        if (thirdPersonFollow == null || !cameraDefaultsCaptured || playerFollowCamera == null) return;

        if (aiming)
        {
            thirdPersonFollow.CameraDistance = aimCameraDistance;
            thirdPersonFollow.CameraSide = aimCameraSide;
            thirdPersonFollow.ShoulderOffset = defaultShoulderOffset * aimShoulderOffsetScale;
            thirdPersonFollow.VerticalArmLength = aimVerticalArmLength;
            if (adjustFieldOfView)
            {
                var lens = playerFollowCamera.m_Lens;
                lens.FieldOfView = aimFieldOfView;
                playerFollowCamera.m_Lens = lens;
            }
        }
        else
        {
            thirdPersonFollow.CameraDistance = defaultCameraDistance;
            thirdPersonFollow.CameraSide = defaultCameraSide;
            thirdPersonFollow.ShoulderOffset = defaultShoulderOffset;
            thirdPersonFollow.VerticalArmLength = defaultVerticalArmLength;
            if (adjustFieldOfView)
            {
                var lens = playerFollowCamera.m_Lens;
                lens.FieldOfView = defaultFieldOfView;
                playerFollowCamera.m_Lens = lens;
            }
        }
    }

    private void LateUpdate()
    {
        if (DialogueManager.IsBlockingGameplay) return;

        if (aiming && alignBodyYawWithCameraWhileAiming && thirdPersonController != null &&
            thirdPersonController.CinemachineCameraTarget != null)
        {
            Transform pivot = thirdPersonController.CinemachineCameraTarget.transform;
            float targetYaw = pivot.eulerAngles.y;
            float smooth = Mathf.Max(0.0001f, bodyYawAlignSmoothTime);
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref bodyYawVelocity, smooth);

            // Guard against NaN/Infinity causing Quaternion assertion failures.
            if (!float.IsFinite(yaw))
                return;

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        if (thirdPersonFollow == null || !cameraDefaultsCaptured) return;

        float t = Mathf.Clamp01(Time.deltaTime * cameraBlendSpeed);

        float targetDist;
        float targetSide;
        Vector3 targetShoulder;
        float targetArm;
        float targetFov;

        if (aiming)
        {
            targetDist = aimCameraDistance;
            targetSide = aimCameraSide;
            targetShoulder = defaultShoulderOffset * aimShoulderOffsetScale;
            targetArm = aimVerticalArmLength;
            targetFov = aimFieldOfView;
        }
        else
        {
            targetDist = defaultCameraDistance;
            targetSide = defaultCameraSide;
            targetShoulder = defaultShoulderOffset;
            targetArm = defaultVerticalArmLength;
            targetFov = defaultFieldOfView;
        }

        thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, targetDist, t);
        thirdPersonFollow.CameraSide = Mathf.Lerp(thirdPersonFollow.CameraSide, targetSide, t);
        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(thirdPersonFollow.ShoulderOffset, targetShoulder, t);
        thirdPersonFollow.VerticalArmLength = Mathf.Lerp(thirdPersonFollow.VerticalArmLength, targetArm, t);

        if (adjustFieldOfView && playerFollowCamera != null)
        {
            var lens = playerFollowCamera.m_Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, t);
            playerFollowCamera.m_Lens = lens;
        }
    }

    private void ApplyLayerWeightsForAim(bool on)
    {
        if (animator == null || defaultLayerWeights == null) return;

        for (int i = 1; i < animator.layerCount && i < defaultLayerWeights.Length; i++)
            animator.SetLayerWeight(i, on ? 0f : defaultLayerWeights[i]);
    }

    private void OnDisable()
    {
        if (aiming)
            SetAiming(false);
        if (crosshairCanvas != null)
            crosshairCanvas.gameObject.SetActive(false);
    }

    private void EnsureCrosshairBuilt()
    {
        if (crosshairCanvas != null) return;

        GameObject root = new GameObject("AimShootPoint");
        root.transform.SetParent(transform, false);

        crosshairCanvas = root.AddComponent<Canvas>();
        crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        crosshairCanvas.sortingOrder = crosshairCanvasSortOrder;
        crosshairCanvas.overrideSorting = true;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRt = root.GetComponent<RectTransform>();
        canvasRt.anchorMin = Vector2.zero;
        canvasRt.anchorMax = Vector2.one;
        canvasRt.offsetMin = Vector2.zero;
        canvasRt.offsetMax = Vector2.zero;

        Sprite pixel = GetPixelSprite();

        // Center dot — “shoot point”
        CreateUiImage(root.transform, "Dot", Vector2.zero, new Vector2(shootPointDotSize, shootPointDotSize), pixel, shootPointColor);

        // Gap around dot, then four arms (reference: clear center point + lines)
        float gap = shootPointDotSize * 0.5f + 3f;
        float armLen = shootPointCrossLength;
        float halfArm = armLen * 0.5f;
        float th = shootPointCrossThickness;
        float off = gap + halfArm;

        CreateUiImage(root.transform, "CrossL", new Vector2(-off, 0f), new Vector2(armLen, th), pixel, shootPointColor);
        CreateUiImage(root.transform, "CrossR", new Vector2(off, 0f), new Vector2(armLen, th), pixel, shootPointColor);
        CreateUiImage(root.transform, "CrossU", new Vector2(0f, off), new Vector2(th, armLen), pixel, shootPointColor);
        CreateUiImage(root.transform, "CrossD", new Vector2(0f, -off), new Vector2(th, armLen), pixel, shootPointColor);

        root.SetActive(false);
    }

    private static void CreateUiImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
    }

    private static Sprite GetPixelSprite()
    {
        if (s_pixelSprite != null) return s_pixelSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.name = "AimCrosshairPixel";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);

        s_pixelSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_pixelSprite;
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hash && p.type == type)
                return true;
        }

        return false;
    }
}
