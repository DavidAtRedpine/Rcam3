using UnityEngine;
using UnityEngine.UI;

namespace Rcam3 {

public sealed class TomatoDebugMonitor : MonoBehaviour
{
    [SerializeField] FrameDecoder _decoder = null;
    [SerializeField] RawImage _color;
    [SerializeField] RawImage _depth;
    RenderTexture _prevColorRT;
    void Update()
    {
        if (_prevColorRT == _decoder.ColorTexture) return;

        _color.texture = _decoder.ColorTexture;
        _depth.texture = _decoder.DepthTexture;
        _prevColorRT = _decoder.ColorTexture;
    }
}

} // namespace Rcam3
