using DG.Tweening;
using UnityEngine;

public sealed class HintManager : MonoBehaviour
{
    public int HintsUsed { get; private set; }

    public void ResetForLevel() => HintsUsed = 0;

    public BrickBlock GetAvailableHint(BrickBlock[,] blocks)
    {
        for (int y = 0; y < blocks.GetLength(1); y++)
        for (int x = 0; x < blocks.GetLength(0); x++)
        {
            BrickBlock block = blocks[x, y];
            if (block != null && HasClearExit(block, blocks)) return block;
        }
        return null;
    }

    public bool ShowHint(BrickBlock[,] blocks)
    {
        BrickBlock block = GetAvailableHint(blocks);
        if (block == null) return false;
        Transform blockTransform = block.transform;
        blockTransform.DOKill();
        Vector3 originalScale = blockTransform.localScale;
        blockTransform.DOScale(originalScale * 1.18f, 0.2f)
            .SetLoops(4, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                blockTransform.localScale = originalScale;
            });
        HintsUsed++;
        return true;
    }

    private static bool HasClearExit(BrickBlock block, BrickBlock[,] blocks)
    {
        Vector2Int cell = block.Cell;
        Vector2Int direction = block.Direction;
        int x = cell.x + direction.x;
        int y = cell.y + direction.y;
        while (x >= 0 && y >= 0 && x < blocks.GetLength(0) && y < blocks.GetLength(1))
        {
            if (blocks[x, y] != null) return false;
            x += direction.x;
            y += direction.y;
        }
        return true;
    }
}
