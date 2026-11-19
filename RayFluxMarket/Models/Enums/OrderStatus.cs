namespace RayFluxMarket.Models.Enums
{
    public static class OrderStatus
    {
        public const string New = "New";
        public const string Paid = "Paid";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";

        // Допустимые переходы между статусами (state machine)
        private static readonly Dictionary<string, string[]> AllowedTransitions = new()
        {
            [New] = new[] { Paid, Cancelled },
            [Paid] = new[] { Processing, Cancelled },
            [Processing] = new[] { Shipped, Cancelled },
            [Shipped] = new[] { Delivered },
            [Delivered] = Array.Empty<string>(),
            [Cancelled] = Array.Empty<string>()
        };

        public static bool CanTransition(string from, string to)
        {
            if (from == to) return false;
            return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
        }
    }
}
