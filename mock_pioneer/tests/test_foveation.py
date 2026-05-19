import sys
import os
import numpy as np
import struct

# Add parent to path to import foveation
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from foveation import encode_dual_payload

def test_foveation_crop_bounds():
    # Mock frame: 640x480
    W, H = 640, 480
    frame = np.zeros((H, W, 3), dtype=np.uint8)
    
    # 1. Top-Left edge
    _, stats1 = encode_dual_payload(frame, gaze_uv=(0.02, 0.98), fovea_size=200)
    assert stats1["crop_x"] == 0, "Top-left X should clamp to 0"
    assert stats1["crop_y"] == 0, "Top-left Y should clamp to 0"
    assert stats1["crop_w"] < 200, f"Top-left crop width should be truncated, got {stats1['crop_w']}"
    assert stats1["crop_h"] < 200, f"Top-left crop height should be truncated, got {stats1['crop_h']}"

    # 2. Centre
    _, stats2 = encode_dual_payload(frame, gaze_uv=(0.5, 0.5), fovea_size=200)
    assert stats2["crop_w"] == 200, "Centre crop width should be full 200"
    assert stats2["crop_h"] == 200, "Centre crop height should be full 200"

    # 3. Bottom-Right edge
    _, stats3 = encode_dual_payload(frame, gaze_uv=(0.98, 0.02), fovea_size=200)
    assert stats3["crop_x"] > W - 200, "Bottom-right X should be pushed right"
    assert stats3["crop_y"] > H - 200, "Bottom-right Y should be pushed down"
    assert stats3["crop_w"] < 200, f"Bottom-right crop width should be truncated, got {stats3['crop_w']}"
    assert stats3["crop_h"] < 200, f"Bottom-right crop height should be truncated, got {stats3['crop_h']}"
    assert stats3["crop_x"] + stats3["crop_w"] == W, "Should exactly hit right edge"
    assert stats3["crop_y"] + stats3["crop_h"] == H, "Should exactly hit bottom edge"
