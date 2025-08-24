namespace NetVox.Core.Utils
{
    public static class ModulationBuilder
    {
        public static uint Build(ushort system, byte major, byte detail)
        {
            return (uint)((system << 16) | (major << 8) | detail);
        }

        public static uint Default()
        {
            // System = 1 (Audio), Major = 3 (PCM), Detail = 1 (μ-law)
            return Build(1, 3, 1);
        }
    }
}
