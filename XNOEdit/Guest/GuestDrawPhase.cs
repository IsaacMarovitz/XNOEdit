namespace XNOEdit.Guest
{
    public enum GuestDrawPhase
    {
        Opaque,
        PunchThrough,
        Sky,
        Transparent,
    }

    public static class GuestDrawPhases
    {
        /// <summary>
        /// Submission order, following render_main.lua: the opaque and punchthrough
        /// sweeps with blending locked off, then the sky world, then transparency.
        /// </summary>
        public static readonly GuestDrawPhase[] Ordered =
        [
            GuestDrawPhase.Opaque,
            GuestDrawPhase.PunchThrough,
            GuestDrawPhase.Sky,
            GuestDrawPhase.Transparent,
        ];
    }
}
