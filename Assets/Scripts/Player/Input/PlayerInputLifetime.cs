/// <summary>
/// Creates and releases <see cref="PlayerInput"/> wrappers safely.
///
/// The generated <c>PlayerInput.Dispose()</c> only destroys the action asset; it does not disable the
/// action maps first. A map left enabled (PauseMenu enables Motor/Pause, Input enables everything) then
/// trips the generated finalizer's "PlayerInput.Motor.Disable() has not been called" assert when the
/// garbage collector runs, and keeps the actions registered with the Input System until then.
/// Always release through here instead of calling Dispose directly. The generated file is not edited
/// because the Input System regenerates it whenever PlayerInput.inputactions changes.
/// </summary>
public static class PlayerInputLifetime
{
    /// <summary>New wrapper with the player's saved key bindings applied.</summary>
    public static PlayerInput Create()
    {
        var input = new PlayerInput();
        KeyBindings.Register(input.asset);
        return input;
    }

    /// <summary>Unregisters, disables every action map, destroys the asset and clears the reference.</summary>
    public static void Release(ref PlayerInput input)
    {
        if (input == null)
        {
            return;
        }

        KeyBindings.Unregister(input.asset);

        // The maps are plain managed objects: disabling works even when the asset object was already
        // destroyed/unloaded during scene teardown, and the finalizer reads exactly this state.
        try
        {
            input.Disable();
        }
        catch (System.Exception)
        {
            // Asset torn down beyond use: fall back to the one map the finalizer checks.
            if (input.Motor.enabled)
            {
                input.Motor.Disable();
            }
        }

        if (input.asset != null)
        {
            input.Dispose();
        }

        System.GC.SuppressFinalize(input); // released properly: nothing left for the finalizer to check
        input = null;
    }
}
