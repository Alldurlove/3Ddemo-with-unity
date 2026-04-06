using UnityEngine;

[DisallowMultipleComponent]
public class CrosshairCursorController : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] bool lockAndHideOnStart = true;
    [SerializeField] bool relockOnFocus = true;

    [Header("Crosshair")]
    [SerializeField] bool showCrosshair = true;
    [SerializeField] Color crosshairColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] float lineLength = 12f;
    [SerializeField] float lineThickness = 2f;
    [SerializeField] float centerGap = 4f;

    static Texture2D _whiteTex;

    void Start()
    {
        if (lockAndHideOnStart)
            ApplyLockedCursor();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && relockOnFocus && lockAndHideOnStart)
            ApplyLockedCursor();
    }

    void OnGUI()
    {
        if (!showCrosshair)
            return;

        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float t = lineThickness;
        float g = centerGap;
        float len = lineLength;

        Color old = GUI.color;
        GUI.color = crosshairColor;

        // top
        GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - g - len, t, len), _whiteTex);
        // bottom
        GUI.DrawTexture(new Rect(cx - t * 0.5f, cy + g, t, len), _whiteTex);
        // left
        GUI.DrawTexture(new Rect(cx - g - len, cy - t * 0.5f, len, t), _whiteTex);
        // right
        GUI.DrawTexture(new Rect(cx + g, cy - t * 0.5f, len, t), _whiteTex);

        GUI.color = old;
    }

    public static void ApplyLockedCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
