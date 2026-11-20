namespace PenLabelerAssist;

internal static class BatchLiquidTransferPatch
{
	public static bool Prefix(GameItem container, GameItem chemicalItem, int volumeInPart)
	{
		return !ChemBatch.TryBatchLiquidTransfer(container, chemicalItem, volumeInPart);
	}
}
