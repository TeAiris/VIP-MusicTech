using UnityEngine;
using UnityEngine.UI;

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>World or screen UI panel shown when the player enters a studio area.</summary>
    public sealed class AreaPopupPanel : MonoBehaviour
    {
        [SerializeField] private StudioAreaId areaId;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Graphic accentBar;
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private bool billboardToCamera = true;

        private float targetAlpha;
        private Camera viewCamera;

        public StudioAreaId AreaId => areaId;

        public void Configure(StudioAreaId id, Text title, Text body, CanvasGroup group, Graphic accent = null)
        {
            areaId = id;
            titleLabel = title;
            bodyLabel = body;
            canvasGroup = group;
            accentBar = accent;
            HideImmediate();
        }

        public void ApplyContent(string title, string body)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (bodyLabel != null) bodyLabel.text = body;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            targetAlpha = 1f;
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            targetAlpha = 0f;
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        }

        public void HideImmediate()
        {
            targetAlpha = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            viewCamera = Camera.main;
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
                if (canvasGroup.alpha <= 0.001f && targetAlpha <= 0f && gameObject.activeSelf)
                {
                    // Keep active so fade can reverse quickly on re-enter; only disable when fully hidden if desired.
                }
            }

            if (!billboardToCamera || targetAlpha <= 0f) return;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            Vector3 forward = transform.position - viewCamera.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
