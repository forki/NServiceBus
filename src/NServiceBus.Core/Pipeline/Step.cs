namespace NServiceBus.Pipeline
{
    using System;

    /// <summary>
    /// Pipeline step
    /// </summary>
    public struct Step
    {
        /// <summary>
        /// Session identifier. 
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Step identifier.
        /// </summary>
        public string StepId { get; internal set; }

        /// <summary>
        /// Behavior type.
        /// </summary>
        public Type Behavior { get; internal set; }
    }
}