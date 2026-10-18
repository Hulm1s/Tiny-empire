namespace Tycoon.Stations
{
    /// <summary>
    /// A bin a hired hand may stand in: the salon's hair bin, where the cleaner takes the hair bags.
    ///
    /// The ordinary bin (<see cref="DiscardStation"/>) is never workerCompatible, because a worker standing in a
    /// bin would quietly destroy the load it was paid to deliver. Here destroying the load IS the job - the
    /// cleaner carries nothing but hair bags - and only a worker whose route names this square ever uses it
    /// (workers only enter the two squares of their current route). For the player it is the same bin as every other.
    /// </summary>
    public class WorkerBinStation : DiscardStation
    {
        protected override void Awake()
        {
            base.Awake();
            workerCompatible = true;
        }
    }
}
