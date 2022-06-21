using System;
using ConnectorLib.JSON;
using Serilog;

namespace Impostor.Server.WarpWorld.CrowdControl;

public abstract class Effect
{
    private static readonly ILogger Log = Serilog.Log.ForContext<CrowdControlManager>();

    private readonly Guid _instance_id = Guid.NewGuid();

    /// <summary>
    /// The safe name for the effect.
    /// </summary>
    public abstract string Code { get; }

    /// <summary>
    /// The unique numeric ID for the effect.
    /// </summary>
    public abstract uint ID { get; }

    private readonly object _activity_lock = new();

    /// <summary>
    /// The type of effect described by the implementing class.
    /// </summary>
    public virtual EffectType Type { get; } = EffectType.Instant;

    /// <summary>
    /// The duration of the effect. This is only meaningful for timed effects.
    /// </summary>
    public virtual TimeSpan Duration { get; } = TimeSpan.Zero;

    //public virtual string? Group { get; }

    /// <summary>
    /// The mutex set declared by the effect. Effects containing the same mutex will not run concurrently.
    /// </summary>
    public virtual string[] Mutex { get; } = new string[0];

    public enum EffectType : byte
    {
        Instant = 0,
        Timed = 1,
        BidWar = 2
    }

    public bool Active { get; private set; }
    
    /// <summary>
    /// This is executed once for the lifetime of the scheduler.
    /// </summary>
    public virtual void Load() => Log.Debug($"{GetType().Name} was loaded. [{_instance_id:D}]");

    /// <summary>
    /// This is executed once at the end of the scheduler's lifetime.
    /// </summary>
    public virtual void Unload() => Log.Debug($"{GetType().Name} was unloaded. [{_instance_id:D}]");

    /// <summary>
    /// Starts the effect. This is executed every time an effect begins.
    /// </summary>
    /// <returns>True if the effect was successfully started, false otherwise.</returns>
    /// <remarks>It is safe to block here.</remarks>
    public virtual bool Start(Request context)
    {
        Log.Debug($"{GetType().Name} was started. [{_instance_id:D}]");
        return true;
    }

    /// <summary>
    /// Stops the effect. This is executed every time an effect ends.
    /// </summary>
    /// <returns>True if the effect was successfully stopped, false otherwise.</returns>
    /// <remarks>It is safe to block here.</remarks>
    public virtual bool Stop()
    {
        Log.Debug($"{GetType().Name} was stopped. [{_instance_id:D}]");
        return true;
    }

    /// <summary>
    /// Determines if the effect can presently start.
    /// </summary>
    /// <returns>True if the effect may start now, false otherwise.</returns>
    /// <remarks>
    /// It is expected that this value may change in a non-threadsafe fashion.
    /// Start() may still return false even if IsReady() returns true.
    /// </remarks>
    public abstract bool IsReady();

    /// <summary>
    /// Starts the effect in a thread-safe fashion.
    /// </summary>
    /// <returns>True if the effect was started, false otherwise.</returns>
    public bool TryStart(Request context)
    {
        lock (_activity_lock)
        {
            if (Active || (!IsReady())) { return false; }
            return Active = Start(context);
        }
    }

    /// <summary>
    /// Stops the effect in a thread-safe fashion.
    /// </summary>
    /// <returns>True if the effect was successfully (or already) stopped, false otherwise.</returns>
    public bool TryStop()
    {
        lock (_activity_lock)
        {
            if (!Active) { return true; }
            bool result = Stop();
            Active = (!result);
            return result;
        }
    }
}
