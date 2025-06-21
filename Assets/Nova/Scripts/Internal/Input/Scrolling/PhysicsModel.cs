// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal interface IPhysicsModel
    {
        double GetPositionAtTime(double time);
        double GetDerivAtTime(double time);
    }

    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal interface IPhysicsSimulation : IPhysicsModel
    {
        bool IsDone(double time);
    }
}
