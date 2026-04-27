namespace pks5.Services;

public static class ProductionCalculator
{
    public static int CalculateProductionMinutes(int quantity, int minutesPerUnit, decimal efficiencyFactor)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (minutesPerUnit <= 0) throw new ArgumentOutOfRangeException(nameof(minutesPerUnit));
        if (efficiencyFactor <= 0) throw new ArgumentOutOfRangeException(nameof(efficiencyFactor));

        var minutes = (decimal)quantity * minutesPerUnit / efficiencyFactor;
        return (int)Math.Ceiling(minutes);
    }
}

