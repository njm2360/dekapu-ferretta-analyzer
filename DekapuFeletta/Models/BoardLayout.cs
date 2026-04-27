namespace DekapuFeletta.Models;

public static class BoardLayout
{
    public const int Size = 5;
    public const int CellCount = 25;
    public const int CenterNumber = 13;
    public const int CenterIndex = 12;
    public const uint CenterBit = 1u << CenterIndex;

    public static int IndexOf(int number) => number - 1;
    public static int NumberOf(int index) => index + 1;

    public static int RowOf(int number) => (number - 1) % Size;
    public static int ColOf(int number) => (number - 1) / Size;

    public static int NumberAt(int row, int col) => col * Size + row + 1;

    public static uint Bit(int number) => 1u << (number - 1);

    public static readonly uint[] LineMasks = BuildLineMasks();

    private static uint[] BuildLineMasks()
    {
        var lines = new List<uint>(12);

        for (int r = 0; r < Size; r++)
        {
            uint mask = 0;
            for (int c = 0; c < Size; c++)
                mask |= Bit(NumberAt(r, c));
            lines.Add(mask);
        }

        for (int c = 0; c < Size; c++)
        {
            uint mask = 0;
            for (int r = 0; r < Size; r++)
                mask |= Bit(NumberAt(r, c));
            lines.Add(mask);
        }

        uint diag1 = 0;
        uint diag2 = 0;
        for (int i = 0; i < Size; i++)
        {
            diag1 |= Bit(NumberAt(i, i));
            diag2 |= Bit(NumberAt(i, Size - 1 - i));
        }
        lines.Add(diag1);
        lines.Add(diag2);

        return lines.ToArray();
    }
}
