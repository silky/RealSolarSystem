using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;
using System.IO;

namespace RealSolarSystem
{
    // Checks to make sure useLegacyAtmosphere didn't get munged with
    // Could become a general place to prevent RSS changes from being reverted when our back is turned.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RSSWatchDog : MonoBehaviour
    {
        ConfigNode RSSSettings = null;
        int updateCount = 0;
        bool useKeypressClip = false;
        public void Start()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("REALSOLARSYSTEM"))
                RSSSettings = node;

            if (RSSSettings != null)
            {
                RSSSettings.TryGetValue("dumpOrbits", ref dumpOrbits);
                RSSSettings.TryGetValue("useKeypressClip", ref useKeypressClip);
            }

            UpdateAtmospheres();
            GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
        }
        public void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
        }

        public void Update()
        {
            if(!useKeypressClip && updateCount > 22)
                return;
            updateCount++;
            if(updateCount < 20 || (useKeypressClip && !Input.GetKeyDown(KeyCode.P)))
                return;
            
            Camera[] cameras = Camera.allCameras;
            string msg = "Far clip planes now";
            string bodyName = FlightGlobals.getMainBody().name;
            foreach (Camera cam in cameras)
            {
                if (cam.name == "Camera 01" || cam.name == "Camera 00")
                {
                    if (useKeypressClip)
                        cam.farClipPlane *= 1.5f;
                    else
                    {
                        float farClip = -1;
                        if (cam.name.Equals("Camera 00"))
                        {
                            RSSSettings.TryGetValue("cam00FarClip", ref farClip);
                            if (RSSSettings.HasNode(bodyName))
                                RSSSettings.GetNode(bodyName).TryGetValue("cam00FarClip", ref farClip);
                        }
                        else
                        {
                            RSSSettings.TryGetValue("cam01FarClip", ref farClip);
                            if (RSSSettings.HasNode(bodyName))
                                RSSSettings.GetNode(bodyName).TryGetValue("cam01FarClip", ref farClip);
                        }
                        if (farClip > 0)
                            cam.farClipPlane = farClip;
                    }

                    msg += "  (" + cam.name + "): " + cam.farClipPlane + ".";
                }
            }
            if(useKeypressClip)
                ScreenMessages.PostScreenMessage(msg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        double counter = 0;
        bool dumpOrbits = false;
        public void FixedUpdate()
        {
            if (!dumpOrbits)
                return;
            counter += TimeWarp.fixedDeltaTime;
            if (counter < 3600)
                return;
            counter = 0;
            if (FlightGlobals.Bodies == null)
            {
                print("**RSS OBTDUMP*** - null body list!");
                return;
            }
            print("**RSS OBTDUMP***");
            int time = (int)Planetarium.GetUniversalTime();
            print("At time " + time + ", " + KSPUtil.PrintDate(time, true, true));
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (body == null || body.orbitDriver == null)
                    continue;
                if (body.orbitDriver.orbit == null)
                    continue;
                Orbit o = body.orbitDriver.orbit;
                print("********* BODY **********");
                print("name = " + body.name + "(" + i + ")");
                Type oType = o.GetType();
                foreach (FieldInfo f in oType.GetFields())
                {
                    if (f == null || f.GetValue(o) == null)
                        continue;
                    print(f.Name + " = " + f.GetValue(o));
                }
            }
        }

        public void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> evt)
        {

        }
        public void UpdateAtmospheres()
        {
            if (RSSSettings != null)
            {
                AtmosphereFromGround[] AFGs = (AtmosphereFromGround[])Resources.FindObjectsOfTypeAll(typeof(AtmosphereFromGround));
                foreach (ConfigNode node in RSSSettings.nodes)
                {
                    foreach (CelestialBody body in FlightGlobals.Bodies)
                    {
                        print("*RSS* checking useLegacyAtmosphere for " + body.GetName());
                        if (node.HasValue("useLegacyAtmosphere"))
                        {
                            bool UseLegacyAtmosphere = true;
                            bool.TryParse(node.GetValue("useLegacyAtmosphere"), out UseLegacyAtmosphere);
                            //print("*RSSWatchDog* " + body.GetName() + ".useLegacyAtmosphere = " + body.useLegacyAtmosphere.ToString());
                            if (UseLegacyAtmosphere != body.useLegacyAtmosphere)
                            {
                                print("*RSSWatchDog* resetting useLegacyAtmosphere to " + UseLegacyAtmosphere.ToString());
                                body.useLegacyAtmosphere = UseLegacyAtmosphere;
                            }
                        }
                        if (node.HasNode("AtmosphereFromGround"))
                        {
                            foreach (AtmosphereFromGround ag in AFGs)
                            {
                                if (ag != null && ag.planet != null)
                                {
                                    if (ag.planet.name.Equals(node.name))
                                    {
                                        RealSolarSystem.UpdateAFG(body, ag, node.GetNode("AtmosphereFromGround"));
                                        print("*RSSWatchDog* reapplying AtmosphereFromGround settings");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}