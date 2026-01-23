namespace SiroccoLobby.Model
{
    /// <summary>
    /// Represents the current phase of captain mode
    /// </summary>
    public enum CaptainModePhase
    {
        /// <summary>
        /// Captain mode is not active
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Host is assigning captains for each team
        /// </summary>
        AssigningCaptains = 1,
        
        /// <summary>
        /// Captains are picking players in snake draft order
        /// </summary>
        Drafting = 2,
        
        /// <summary>
        /// Draft is complete, ready flow can proceed normally
        /// </summary>
        Complete = 3
    }
}
