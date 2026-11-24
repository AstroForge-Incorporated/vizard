/*
 ISC License

 Copyright (c) 2025, Autonomous Vehicle Systems Lab, University of Colorado at Boulder

 Permission to use, copy, modify, and/or distribute this software for any
 purpose with or without fee is hereby granted, provided that the above
 copyright notice and this permission notice appear in all copies.

 THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

 */
using VizProtobufferMessage;
using System.IO;
using UnityEngine;
/// <summary>
/// Static class that maintains the current persistent user settings, saves them for
/// the next session, loads current scenario's settings and handles resetting the
/// persistent settings to the viz defaults.
/// </summary>
public static class PersistentUserSettings
{
    internal const int DefaultGUIReferenceHeight = 900;
    private static string persistentSettingsPath = Application.persistentDataPath+"/VizardPersistentSettings.bin";
    public static VizMessage.Types.VizSettingsPb persistentSettingsFromLastSave = new VizMessage.Types.VizSettingsPb()
    {
    };
    
    public static VizMessage.Types.VizSettingsPb currentSessionUserAppliedSettings = new VizMessage.Types.VizSettingsPb()
    {
    };

    private static readonly VizMessage.Types.VizSettingsPb defaultSettings = new VizMessage.Types.VizSettingsPb()
    {
        //General Settings
        Ambient = 0.3,
        SpacecraftShadowBrightness = 0.25,
        SunIntensity = 1,
        AttenuateSunLightWithDistance = -1,
        CustomGUIReferenceHeight = DefaultGUIReferenceHeight,
        SpacecraftSizeMultiplier = 5,
        SpacecraftHelioViewSizeMultiplier = 5,
        CelestialBodyHelioViewSizeMultiplier = 10,
        //Camera Settings
        KeyboardAngularRate = 2,
        KeyboardZoomRate = 1,
        //Orbit Lines Settings
        OrbitLineSegments = 180,
        RelativeOrbitRange = 360,
        RelativeOrbitFrame = 1,
        SpacecraftOrbitLineWidth = 1,
        CelestialBodyOrbitLineWidth = 1,
        OsculatingOrbitLineRange = { -180, 180 },
        LinesAndFramesLineWidth = 1,
        UseLineRenderersForTargetLinesAndFrames = 1,
        //Ground Track Settings
        OsculatingGroundTrackRange = { -180, 180 },
        //Actuators
        DefaultThrusterColor = { 255,255,255,255 },
        DefaultThrusterPlumeLifeScalar = 1,
        //Sprites
        DefaultSpacecraftSprite = "Circle 255 255 255 255"
    };
    public static void WritePersistentSettings()
    {
        if (!Directory.Exists(Path.GetDirectoryName(persistentSettingsPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(persistentSettingsPath));
        }

        VizMessage messageToWrite = new VizMessage();
        messageToWrite.Settings = currentSessionUserAppliedSettings;
        using (var output = File.Create(persistentSettingsPath))
        {
            Google.Protobuf.MessageExtensions.WriteDelimitedTo(messageToWrite, output);
        }
    }

    public static void ReadPersistentSettings()
    {
        if (File.Exists(persistentSettingsPath))
        {
            using (FileStream msgFile =
                   File.Open(persistentSettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                VizMessage message = VizMessage.Parser.ParseDelimitedFrom(msgFile);
                persistentSettingsFromLastSave = message.Settings;
            }
        }
        else
        {
            persistentSettingsFromLastSave = defaultSettings.Clone();
        }

        currentSessionUserAppliedSettings = persistentSettingsFromLastSave.Clone();
    }

    public static void RestoreVizDefaultSettings()
    { 
        //Can I just use the UserGUISettings method and send a reset settings message by pushing a button?
        
        persistentSettingsFromLastSave = defaultSettings.Clone();
        currentSessionUserAppliedSettings = defaultSettings.Clone();
        SetOrbitLineSegmentsPer360(persistentSettingsFromLastSave.OrbitLineSegments,false);
    }

    public static void SetOrbitLineSegmentsPer360(int newSegmentCount, bool setByUser)
    {
        persistentSettingsFromLastSave.OrbitLineSegments = newSegmentCount;
        if (setByUser)
        {
            currentSessionUserAppliedSettings.OrbitLineSegments = newSegmentCount;
        }

        foreach(GameObject line in CelestialBodyStateUtilities.CelestialBodyOrbitLines)
        {
            line.GetComponent<OsculatingOrbitLine>().UpdateOrbitLineSegmentCountAndOrbitRange();
        }

        foreach(GameObject line in SpacecraftStateUtilities.SpacecraftOrbitLinesList){
            line.GetComponent<OsculatingOrbitLine>().UpdateOrbitLineSegmentCountAndOrbitRange();
            line.GetComponentInChildren<GroundTrackOsculating>().UpdateOrbitLineSegmentCountAndGroundTrackRange();
        }
    }
    
    public static void SetOsculatingOrbitDegreeRange(int newRangeStart, int newRangeEnd, bool setByUser)
    {
        if (currentSessionUserAppliedSettings.OsculatingOrbitLineRange.Count < 2)
        {
            currentSessionUserAppliedSettings.OsculatingOrbitLineRange.Add(defaultSettings.OsculatingOrbitLineRange[0]);
            currentSessionUserAppliedSettings.OsculatingOrbitLineRange.Add(defaultSettings.OsculatingOrbitLineRange[1]);
        }

        
        if (persistentSettingsFromLastSave.OsculatingOrbitLineRange.Count >= 2)
        {
            persistentSettingsFromLastSave.OsculatingOrbitLineRange[0] = newRangeStart;
            persistentSettingsFromLastSave.OsculatingOrbitLineRange[1] = newRangeEnd;
        }
        else
        {
            persistentSettingsFromLastSave.OsculatingOrbitLineRange.Add(newRangeStart);
            persistentSettingsFromLastSave.OsculatingOrbitLineRange.Add(newRangeEnd);
        }

        if (setByUser)
        {
            //currentUserAppliedSettings.RelativeOrbitRange = newDegreeRange;
            currentSessionUserAppliedSettings.OsculatingOrbitLineRange[0] = newRangeStart;
            currentSessionUserAppliedSettings.OsculatingOrbitLineRange[1] = newRangeEnd;
        }

        foreach(GameObject line in SpacecraftStateUtilities.SpacecraftOrbitLinesList){
            line.GetComponent<OsculatingOrbitLine>().UpdateOrbitLineSegmentCountAndOrbitRange();
        }
    }
    
    public static void SetOsculatingGroundTrackDegreeRange(int newRangeStart, int newRangeEnd, bool setByUser)
    {   

        if (currentSessionUserAppliedSettings.OsculatingGroundTrackRange.Count < 2)
        {
            currentSessionUserAppliedSettings.OsculatingGroundTrackRange.Add(defaultSettings.OsculatingGroundTrackRange[0]);
            currentSessionUserAppliedSettings.OsculatingGroundTrackRange.Add(defaultSettings.OsculatingGroundTrackRange[1]);
        }

        if (persistentSettingsFromLastSave.OsculatingGroundTrackRange.Count >= 2)
        {
            persistentSettingsFromLastSave.OsculatingGroundTrackRange[0] = newRangeStart;
            persistentSettingsFromLastSave.OsculatingGroundTrackRange[1] = newRangeEnd;
        }
        else
        {
            persistentSettingsFromLastSave.OsculatingGroundTrackRange.Add(newRangeStart);
            persistentSettingsFromLastSave.OsculatingGroundTrackRange.Add(newRangeEnd);
        }

        if (setByUser)
        {
            currentSessionUserAppliedSettings.OsculatingGroundTrackRange[0] = newRangeStart;
            currentSessionUserAppliedSettings.OsculatingGroundTrackRange[1] = newRangeEnd;
        }

        foreach(GameObject line in SpacecraftStateUtilities.SpacecraftOrbitLinesList){
            line.GetComponentInChildren<GroundTrackOsculating>().UpdateOrbitLineSegmentCountAndGroundTrackRange();
        }
    }

    public static void SetAttenuateSunLightWithDistance(bool isOn, bool setByUser)
    {
        int value = isOn ? 1 : -1;
        persistentSettingsFromLastSave.AttenuateSunLightWithDistance =
            value;
        if (setByUser)
        {
            currentSessionUserAppliedSettings.AttenuateSunLightWithDistance = value;
        }
        if (!isOn)
        {
            SetSunOrMainLightIntensity((float)persistentSettingsFromLastSave.SunIntensity, false);
        }
    }
    
    public static void SetSunOrMainLightIntensity(float newIntensity, bool setByUser)
    {
        if (VizardGUISettings.MainLight == null)
        {
            VizardGUISettings.MainLight = GameObject.FindWithTag(CelestialBodyStateUtilities.SunMsgAvailable ? "Sun" : "DefaultMainLight");
        }
        if (CelestialBodyStateUtilities.SunMsgAvailable){
            VizardGUISettings.MainLight.GetComponent<SunBuilder>().SetSunIntensityAtEarth(newIntensity);
            
        }else{
            VizardGUISettings.MainLight.GetComponent<Light>().intensity = newIntensity;
        }
        persistentSettingsFromLastSave.SunIntensity = newIntensity;
        if (setByUser)
        {
            currentSessionUserAppliedSettings.SunIntensity = newIntensity;
        }
    }

    public static void SetSpacecraftShaderEmissive(float newValue, bool setByUser)
    {
        persistentSettingsFromLastSave.SpacecraftShadowBrightness = newValue;
        if (setByUser)
        {
            currentSessionUserAppliedSettings.SpacecraftShadowBrightness = newValue;
        }
        foreach(GameObject sc in SpacecraftStateUtilities.SpacecraftList){
            sc.GetComponent<SpacecraftController>().ApplyCurrentEmissionSetting();
        }
    }

    public static void SetDefaultSpacecraftSprite(string spriteSetting, bool setByUser)
    {
        persistentSettingsFromLastSave.DefaultSpacecraftSprite = spriteSetting;
        if (setByUser)
        {
            currentSessionUserAppliedSettings.DefaultSpacecraftSprite = spriteSetting;
        }

        foreach (GameObject sc in SpacecraftStateUtilities.ParentSpacecraftList)
        {
            sc.GetComponent<SpacecraftController>().UpdateDefaultSprite(spriteSetting);
        }
    }
}
