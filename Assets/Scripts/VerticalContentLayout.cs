using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class VerticalContentLayout : MonoBehaviour
{
    [Header("Padding & Spacing")]
    public float topPadding = 8f;
    public float bottomPadding = 8f;
    public float leftPadding = 8f;
    public float rightPadding = 8f;
    public float spacing = 8f;

    [Header("Options")]
    public bool includeInactive = false;
    public bool usePreferredHeight = true;  // read LayoutElement/ContentSizeFitter heights
    public bool stretchChildWidth = true;   // stretch to content width
    public bool reverseOrder = false;       // last child on top if true

    RectTransform _content;
    bool _isRebuilding;
    bool _pending;

    void OnEnable()
    {
        _content = GetComponent<RectTransform>();
        EnsureTopAnchored();
        RequestRebuild();
    }

    void OnTransformChildrenChanged() => RequestRebuild();

    void OnRectTransformDimensionsChange()
    {
        // This event fires when we change sizeDelta ourselves.
        // If we're already rebuilding, ignore to prevent recursion.
        if (_isRebuilding) return;
        RequestRebuild();
    }

    void LateUpdate()
    {
        if (_pending)
        {
            _pending = false;
            Rebuild();
        }
    }

    public void RequestRebuild() => _pending = true;

    void EnsureTopAnchored()
    {
        // Top-anchored content is assumed by this layout.
        if (_content == null) return;
        if (!Application.isPlaying)
        {
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
        }
    }

    public void Rebuild()
    {
        if (_isRebuilding || _content == null) return;
        _isRebuilding = true;
        try
        {
            float y = topPadding;
            int childCount = _content.childCount;

            // Determine order
            int start = reverseOrder ? childCount - 1 : 0;
            int end = reverseOrder ? -1 : childCount;
            int step = reverseOrder ? -1 : 1;

            for (int i = start; i != end; i += step)
            {
                Transform t = _content.GetChild(i);
                if (!includeInactive && !t.gameObject.activeSelf) continue;

                var rt = t as RectTransform;
                if (rt == null) continue;

                if (stretchChildWidth)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    // keep height in sizeDelta.y; only adjust horizontal offsets
                    var oMin = rt.offsetMin; oMin.x = leftPadding; rt.offsetMin = oMin;
                    var oMax = rt.offsetMax; oMax.x = -rightPadding; rt.offsetMax = oMax;
                }

                float h = 0f;
                if (usePreferredHeight)
                {
                    // Query preferred first; if zero, fall back to min/rect
                    h = Mathf.Max(
                        LayoutUtility.GetPreferredHeight(rt),
                        LayoutUtility.GetMinHeight(rt),
                        rt.rect.height
                    );
                }
                else
                {
                    h = Mathf.Max(rt.rect.height, 0f);
                }

                // Apply computed height if using preferred sizing
                if (usePreferredHeight)
                {
                    var sz = rt.sizeDelta;
                    sz.y = h;
                    rt.sizeDelta = sz;
                }

                // Position from top; anchored Y goes negative as we go down
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);

                y += h + spacing;
            }

            // Final content height (avoid writing if unchanged to reduce churn)
            float targetHeight = Mathf.Max(0f, y - spacing + bottomPadding);
            if (!Mathf.Approximately(_content.sizeDelta.y, targetHeight))
            {
                _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            }
        }
        finally
        {
            _isRebuilding = false;
        }
    }
}
