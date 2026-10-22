using UnityEngine;

namespace KnockoutBalance
{
    [RequireComponent(typeof(Camera))]
    public sealed class DynamicCamera : MonoBehaviour
    {
        [SerializeField] private Transform player1;
        [SerializeField] private Transform player2;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 8f;
        [SerializeField] private float zoomLimiter = 10f;
        [SerializeField] private float smoothTime = 0.2f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
        [SerializeField] private float deathYThreshold = -5f;
        [SerializeField] private SpriteRenderer fixedBackground;
        [SerializeField, Min(1f)] private float backgroundPadding = 1.05f;
        [SerializeField] private SpriteRenderer displayBorder;
        [SerializeField, Min(1f)] private float borderOverscan = 1.04f;
        [SerializeField, Min(1f)] private float borderVerticalScale = 1.03f;

        private Camera trackedCamera;
        private Vector3 moveVelocity;
        private float zoomVelocity;
        private float lastBackgroundAspect = -1f;
        private float lastBorderAspect = -1f;
        private float lastBorderZoom = -1f;

        public void Configure(
            Transform firstPlayer,
            Transform secondPlayer,
            SpriteRenderer background,
            SpriteRenderer border)
        {
            player1 = firstPlayer;
            player2 = secondPlayer;
            fixedBackground = background;
            displayBorder = border;
        }

        private void Awake()
        {
            trackedCamera = GetComponent<Camera>();
            trackedCamera.orthographic = true;
            ResizeBackground();
            ResizeDisplayBorder();
        }

        private void LateUpdate()
        {
            if (trackedCamera == null) return;

            bool trackPlayer1 = IsTrackable(player1);
            bool trackPlayer2 = IsTrackable(player2);
            Vector3 focusPoint;
            float targetZoom;

            if (trackPlayer1 && trackPlayer2)
            {
                focusPoint = (player1.position + player2.position) * 0.5f;
                float distance = Vector2.Distance(player1.position, player2.position);
                float zoomAmount = Mathf.Clamp01(distance / Mathf.Max(0.01f, zoomLimiter));
                targetZoom = Mathf.Lerp(minZoom, maxZoom, zoomAmount);
            }
            else if (trackPlayer1 || trackPlayer2)
            {
                // Once a fighter falls below the boundary, ignore that fighter
                // so the camera does not chase them away from the platform.
                focusPoint = trackPlayer1 ? player1.position : player2.position;
                targetZoom = minZoom;
            }
            else
            {
                // During the brief interval where both objects are below the
                // threshold, keep the current framing until the round respawns.
                focusPoint = transform.position - offset;
                targetZoom = minZoom;
            }

            Vector3 targetPosition = focusPoint + offset;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref moveVelocity,
                Mathf.Max(0.01f, smoothTime));

            trackedCamera.orthographicSize = Mathf.SmoothDamp(
                trackedCamera.orthographicSize,
                targetZoom,
                ref zoomVelocity,
                Mathf.Max(0.01f, smoothTime));

            if (!Mathf.Approximately(lastBackgroundAspect, trackedCamera.aspect))
                ResizeBackground();
            if (!Mathf.Approximately(lastBorderAspect, trackedCamera.aspect)
                || !Mathf.Approximately(lastBorderZoom, trackedCamera.orthographicSize))
                ResizeDisplayBorder();
        }

        private bool IsTrackable(Transform target)
        {
            return target != null && target.gameObject.activeInHierarchy && target.position.y >= deathYThreshold;
        }

        private void ResizeBackground()
        {
            if (trackedCamera == null || fixedBackground == null || fixedBackground.sprite == null) return;

            Transform backgroundTransform = fixedBackground.transform;
            if (backgroundTransform.parent != transform)
                backgroundTransform.SetParent(transform, true);

            Vector3 localPosition = backgroundTransform.localPosition;
            localPosition.x = 0f;
            localPosition.y = 0f;
            if (Mathf.Abs(localPosition.z) < 1f) localPosition.z = 100f;
            backgroundTransform.localPosition = localPosition;
            backgroundTransform.localRotation = Quaternion.identity;

            Vector2 spriteSize = fixedBackground.sprite.bounds.size;
            float requiredHeight = maxZoom * 2f * backgroundPadding;
            float requiredWidth = requiredHeight * trackedCamera.aspect;
            float scale = Mathf.Max(
                requiredWidth / Mathf.Max(0.01f, spriteSize.x),
                requiredHeight / Mathf.Max(0.01f, spriteSize.y));
            backgroundTransform.localScale = new Vector3(scale, scale, 1f);
            lastBackgroundAspect = trackedCamera.aspect;
        }

        private void ResizeDisplayBorder()
        {
            if (trackedCamera == null || displayBorder == null || displayBorder.sprite == null) return;

            Transform borderTransform = displayBorder.transform;
            if (borderTransform.parent != transform)
                borderTransform.SetParent(transform, true);

            Vector2 spriteSize = displayBorder.sprite.bounds.size;
            float viewHeight = trackedCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * trackedCamera.aspect;
            // Slightly exceed the viewport so transparent padding and uneven
            // outer pixels in the artwork are cropped beyond the screen edge.
            float scaleX = viewWidth / Mathf.Max(0.01f, spriteSize.x) * borderOverscan;
            float scaleY = viewHeight / Mathf.Max(0.01f, spriteSize.y)
                * borderOverscan
                * borderVerticalScale;
            borderTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            borderTransform.localRotation = Quaternion.identity;

            // border_0 uses a bottom-left pivot. Offset its scaled bounds so
            // the visual center remains exactly on the camera center.
            Vector2 spriteCenter = displayBorder.sprite.bounds.center;
            borderTransform.localPosition = new Vector3(
                -spriteCenter.x * scaleX,
                -spriteCenter.y * scaleY,
                1f);
            displayBorder.sortingOrder = 1000;
            lastBorderAspect = trackedCamera.aspect;
            lastBorderZoom = trackedCamera.orthographicSize;
        }
    }
}
