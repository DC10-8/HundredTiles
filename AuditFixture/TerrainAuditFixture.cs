namespace HundredTiles.AuditFixture
{
    public static class TerrainAuditFixture
    {
        public static int Resolution() => 1080;
        public static float WorldWidth() => 17280f;
        public static int PatchSide() => 9;
        public static int[] NewBuffer() => new int[1168561];
        public static int Call() => Resolution();
        // IL switch operands must be skipped as a table rather than as opcodes.
        public static int Branch(int value)
        {
            switch (value)
            {
                case 0: return 1080;
                case 1: return 1081;
                case 2: return 81;
                case 3: return 8640;
                case 4: return 17;
                default: return -1;
            }
        }
    }
}
