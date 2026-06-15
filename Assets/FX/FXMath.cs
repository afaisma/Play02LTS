using UnityEngine;

// Pure positioning math, factored out of FXRuntime so it is unit-testable
// without a scene/Canvas (target-rect math from the spec's "two spots to get right").
public static class FXMath
{
    // Center of a RectTransform's 4 world corners (the array GetWorldCorners fills).
    // Order from Unity: [0]=BL [1]=TL [2]=TR [3]=BR. Average = center.
    public static Vector3 CenterOfCorners(Vector3[] corners)
    {
        if (corners == null || corners.Length < 4)
            return Vector3.zero;
        return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
    }

    // Local offset (in the target rect's own space) for a placement anchor. Used by
    // Decorate to tuck a bubble into a corner of a tile. size = target rect size.
    public static Vector2 PlacementOffset(FXLibrary.FXPlacement placement, Vector2 size, float inset)
    {
        float hx = size.x * 0.5f - inset;
        float hy = size.y * 0.5f - inset;
        switch (placement)
        {
            case FXLibrary.FXPlacement.TopRight:    return new Vector2(hx, hy);
            case FXLibrary.FXPlacement.BottomRight: return new Vector2(hx, -hy);
            case FXLibrary.FXPlacement.TopLeft:     return new Vector2(-hx, hy);
            case FXLibrary.FXPlacement.BottomLeft:  return new Vector2(-hx, -hy);
            default:                                return Vector2.zero; // Center
        }
    }
}
