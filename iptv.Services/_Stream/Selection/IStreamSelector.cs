namespace iptv.Services._Stream.Selection;

public interface IStreamSelector
{
    /// <summary>Filters out ineligible candidates and returns the rest in selection order (best first).</summary>
    IReadOnlyList<StreamCandidate> Order(
        IEnumerable<StreamCandidate> candidates, StreamSelectionContext context);
}
