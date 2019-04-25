/* This content is licensed under the terms of the Creative Commons Attribution 4.0 International License.
 * When using this content, you must:
 * �    Acknowledge that the content is from the Sansar Knowledge Base.
 * �    Include our copyright notice: "� 2017 Linden Research, Inc."
 * �    Indicate that the content is licensed under the Creative Commons Attribution-Share Alike 4.0 International License.
 * �    Include the URL for, or link to, the license summary at https://creativecommons.org/licenses/by-sa/4.0/deed.hi (and, if possible, to the complete license terms at https://creativecommons.org/licenses/by-sa/4.0/legalcode.
 * For example:
 * "This work uses content from the Sansar Knowledge Base. � 2017 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode)."
 */

using Sansar.Simulation;
using Sansar.Script;
using Sansar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptLibrary
{
    [Tooltip("Turn attached scriptable lights on and off in response to simple script events.")]
    [DisplayName("LightOnOff")]
    public class ObjectLightOnOff : ObjectBase
    {
        #region EditorProperties
        [Tooltip("Event name to turn on the light. Can be a comma separated list of event names.")]
        [DefaultValue("on")]
        [DisplayName("-> Turn On")]
        public readonly string TurnOnEvent;

        [Tooltip("The fade time to turn on the light.")]
        [DefaultValue(0.1f)]
        [DisplayName("Turn On Fade Time")]
        [Range(0, 5)]
        public readonly float TurnOnFadeTime;

        [Tooltip("Event name to turn off the light. Can be a comma separated list of event names.")]
        [DefaultValue("off")]
        [DisplayName("-> Turn Off")]
        public readonly string TurnOffEvent;

        [Tooltip("The fade time to turn off the light.")]
        [DefaultValue(0.1f)]
        [DisplayName("Turn Off Fade Time")]
        [Range(0, 5)]
        public readonly float TurnOffFadeTime;

        [Tooltip("If true, the lights will be turned off when the world starts. Otherwise, the lights will be left on.")]
        [DefaultValue(true)]
        [DisplayName("Turn Off At Start")]
        public readonly bool TurnOffAtStart;

        [Tooltip("Enable responding to events for this script. Can be a comma separated list of event names.")]
        [DefaultValue("light_enable")]
        [DisplayName("-> Enable")]
        public readonly string EnableEvent;

        [Tooltip("Disable responding to events for this script. Can be a comma separated list of event names.")]
        [DefaultValue("light_disable")]
        [DisplayName("-> Disable")]
        public readonly string DisableEvent;

        [Tooltip(@"If StartEnabled is true then the script will respond to events when the scene is loaded
If StartEnabled is false then the script will not respond to events until an (-> Enable) event is received.")]
        [DefaultValue(true)]
        [DisplayName("Start Enabled")]
        public readonly bool StartEnabled = true;
        #endregion

        List<LightComponent> lights = null;
        Action subscriptions = null;

        Sansar.Color initialColor;
        float initialIntensity;

        Sansar.Color previousColor;
        float previousIntensity = 0.0f;

        Sansar.Color targetColor;
        float targetIntensity = 0.0f;

        float interpolationTime = 0.0f;
        float interpolationDuration = 0.0f;
        bool interpolationActive = false;
        ICoroutine interpolationCoroutine = null;

        protected override void SimpleInit()
        {
            lights = new List<LightComponent>();

            uint lightCount = ObjectPrivate.GetComponentCount(ComponentType.LightComponent);

            for (uint i = 0; i < lightCount; i++)
            {
                LightComponent lc = (LightComponent)ObjectPrivate.GetComponent(ComponentType.LightComponent, i);
                if (lc.IsScriptable)
                {
                    lights.Add(lc);
                    break;
                }
            }

            if (lights.Count == 0)
            {
                Log.Write(LogLevel.Error, "SimpleLightOnOff::Init", "Object must have at least one scriptable light added at edit time for SimpleLightOnOff script to work.");
                return;
            }

            initialColor = lights[0].GetNormalizedColor();
            initialIntensity = lights[0].GetRelativeIntensity();

            // Turn off the light if specified
            if (TurnOffAtStart) SetColorAndIntensityOfAllLights(Sansar.Color.Black, 0.0f);

            if (StartEnabled) Subscribe(null);

            SubscribeToAll(EnableEvent, Subscribe);
            SubscribeToAll(DisableEvent, Unsubscribe);
        }

        private void SetColorAndIntensityOfAllLights(Sansar.Color c, float intensity)
        {
            foreach (var light in lights)
            {
                light.SetColorAndIntensity(c, intensity);
            }
        }

        private bool HasFadeTime()
        {
            return (TurnOnFadeTime > 0.0f || TurnOffFadeTime > 0.0f);
        }

        private void InterpolateLightColor()
        {
            const float deltaTime = 0.1f;
            TimeSpan ts = TimeSpan.FromSeconds(deltaTime);

            while (true)
            {
                Wait(ts);

                if (interpolationActive)
                {
                    interpolationTime = Math.Max(interpolationTime - deltaTime, 0.0f);

                    float t = interpolationTime / interpolationDuration;

                    Sansar.Color color = previousColor * t + targetColor * (1.0f - t);
                    float intensity = previousIntensity * t + targetIntensity * (1.0f - t);

                    SetColorAndIntensityOfAllLights(color, intensity);

                    interpolationActive = (interpolationTime > 0.0f);
                }
            }
        }

        private void StartInterpolation()
        {
            if ((interpolationCoroutine == null) && HasFadeTime())
            {
                targetColor = previousColor = lights[0].GetNormalizedColor();
                targetIntensity = previousIntensity = lights[0].GetRelativeIntensity();

                interpolationDuration = 0.0f;
                interpolationTime = 0.0f;
                interpolationActive = false;

                interpolationCoroutine = StartCoroutine(InterpolateLightColor);
            }
        }

        private void StopInterpolation()
        {
            if (interpolationCoroutine != null)
            {
                interpolationCoroutine.Abort();
                interpolationCoroutine = null;
            }
        }

        private void Subscribe(ScriptEventData sed)
        {
            if (subscriptions == null)
            {
                subscriptions = SubscribeToAll(TurnOnEvent, (data) =>
                {
                    if (TurnOnFadeTime > 0.0f)
                    {
                        previousColor = lights[0].GetNormalizedColor();
                        previousIntensity = lights[0].GetRelativeIntensity();
                        targetColor = initialColor;
                        targetIntensity = initialIntensity;
                        interpolationDuration = TurnOnFadeTime;
                        interpolationTime = TurnOnFadeTime;
                        interpolationActive = true;
                    }
                    else
                    {
                        interpolationActive = false;
                        SetColorAndIntensityOfAllLights(initialColor, initialIntensity);
                    }
                });

                subscriptions += SubscribeToAll(TurnOffEvent, (data) =>
                {
                    if (TurnOffFadeTime > 0.0f)
                    {
                        previousColor = lights[0].GetNormalizedColor();
                        previousIntensity = lights[0].GetRelativeIntensity();
                        targetColor = Sansar.Color.Black;
                        targetIntensity = 0.0f;
                        interpolationDuration = TurnOffFadeTime;
                        interpolationTime = TurnOffFadeTime;
                        interpolationActive = true;
                    }
                    else
                    {
                        interpolationActive = false;
                        SetColorAndIntensityOfAllLights(Sansar.Color.Black, 0.0f);
                    }
                });
            }

            if (HasFadeTime())
                StartInterpolation();
        }

        private void Unsubscribe(ScriptEventData sed)
        {
            if (subscriptions != null)
            {
                subscriptions();
                subscriptions = null;
            }

            StopInterpolation();
        }
    }
}