using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;

public class ToggleHandler : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        AssignButtonHandler("ToggleMute", (toggle) =>
        {
            IRtcEngine rtc = IRtcEngine.QueryEngine();
            if (rtc != null)
            {
                rtc.MuteLocalAudioStream(toggle.isOn);
            }
        });

        AssignButtonHandler("ToggleCamera", (toggle) =>
        {
            IRtcEngine rtc = IRtcEngine.QueryEngine();
            if (rtc != null)
            {
                rtc.SwitchCamera();
            }
        });

    }

    void AssignButtonHandler(string btname, System.Action<Toggle> handleTap)
    {
        if (gameObject.name != btname)
        {
            return;
        }
        Toggle toggle = gameObject.GetComponent<Toggle>();
        toggle.onValueChanged.AddListener((on) =>
        {
            Debug.Log("Toggle " + btname + " => " + on);
            handleTap(toggle);
        });
    }
}
