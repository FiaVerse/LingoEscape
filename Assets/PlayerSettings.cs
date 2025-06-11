using UnityEngine;

public static class PlayerSettings
{
    public static string Language  = "";   // empty until chosen
    public static string Level     = "";
    public static string Voice     = "";

    public static bool IsComplete =>
        !string.IsNullOrEmpty(Language) &&
        !string.IsNullOrEmpty(Level)    &&
        !string.IsNullOrEmpty(Voice);

    public static void Reset()
    {
        Language = "";
        Level    = "";
        Voice    = "";
    }
}