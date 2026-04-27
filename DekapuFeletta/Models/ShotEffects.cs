namespace DekapuFeletta.Models;

public static class ShotEffects
{
    public static uint GetDeterministicAddMask(ShotType shot, int number)
    {
        int row = BoardLayout.RowOf(number);
        int col = BoardLayout.ColOf(number);

        return shot switch
        {
            ShotType.None => 0u,
            ShotType.Vertical => VerticalNeighbors(row, col),
            ShotType.Horizontal => HorizontalNeighbors(row, col),
            ShotType.Cross => VerticalNeighbors(row, col) | HorizontalNeighbors(row, col),
            ShotType.VerticalLine => ColumnMask(col),
            ShotType.HorizontalLine => RowMask(row),
            ShotType.Giant => GiantMask(row, col),
            _ => 0u,
        };
    }

    private static uint ColumnMask(int col)
    {
        uint mask = 0;
        for (int r = 0; r < BoardLayout.Size; r++)
            mask |= BoardLayout.Bit(BoardLayout.NumberAt(r, col));
        return mask;
    }

    private static uint RowMask(int row)
    {
        uint mask = 0;
        for (int c = 0; c < BoardLayout.Size; c++)
            mask |= BoardLayout.Bit(BoardLayout.NumberAt(row, c));
        return mask;
    }

    private static uint GiantMask(int row, int col)
    {
        uint mask = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                int r = (row + dr + BoardLayout.Size) % BoardLayout.Size;
                int c = (col + dc + BoardLayout.Size) % BoardLayout.Size;
                mask |= BoardLayout.Bit(BoardLayout.NumberAt(r, c));
            }
        }
        return mask;
    }

    private static uint VerticalNeighbors(int row, int col)
    {
        int rowUp = (row - 1 + BoardLayout.Size) % BoardLayout.Size;
        int rowDown = (row + 1) % BoardLayout.Size;
        return BoardLayout.Bit(BoardLayout.NumberAt(rowUp, col))
             | BoardLayout.Bit(BoardLayout.NumberAt(rowDown, col));
    }

    private static uint HorizontalNeighbors(int row, int col)
    {
        int colLeft = (col - 1 + BoardLayout.Size) % BoardLayout.Size;
        int colRight = (col + 1) % BoardLayout.Size;
        return BoardLayout.Bit(BoardLayout.NumberAt(row, colLeft))
             | BoardLayout.Bit(BoardLayout.NumberAt(row, colRight));
    }
}
