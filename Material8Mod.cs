using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using HarmonyLib;
using DarkTonic.MasterAudio;
using PolyPhysics;
using Poly.Math;
using Poly.Physics;
using TMPro;


namespace Material8Mod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class Material8Mod : PolyTechMod
    {
        public new const string
            PluginGuid = "org.bepinex.plugins.Material8Mod",
            PluginName = "Material 8 Mod",
            PluginVersion = "1.0.1";
        
        public static Material8Mod instance;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<float> freeLengthOverride;
        public Sprite bungee;
        public Sprite bungee_selected;
        public TwoStateButton m_BungeeButton;
        public MaterialLimit m_BungeeMaterialLimit;
        public MaterialToolTipText m_BungeeToolTip;
        public SandboxInputField m_BungeeInputField;
        public UnityEvent click;
        public bool startupComplete = false;
        public bool enableQueued = false;
        public bool disableQueued = false;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> bungeeKeyBind;
        public const string resourceFolder = "BungeeRopeResources";
        Harmony harmony;
        void Awake()
        {
            //this.repositoryUrl = "https://github.com/Conqu3red/BungeeRopeMod/"; // repo to check for updates from
			if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = true;
           
            modEnabled = Config.Bind(PluginName, "Mod Enabled", true, "Enable Mod");
            bungeeKeyBind = Config.Bind(PluginName, "Bungee Rope keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.Alpha8));
            
            
            modEnabled.SettingChanged += onEnableDisable;
            onEnableDisable(null, null);
            
            harmony = new Harmony("org.bepinex.plugins.Material8Mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            this.authors = new string[] {"Conqu3red"};

            PolyTechMain.registerMod(this);

            /* -- NOTES -- 
                bungee rope (material 8)
                    edge.m_Material.m_EdgeMaterial.isSpring = true
                    edge.m_SpringCoilVisualization = null
                    it has to be a (BridgeSpring)

                Panel_Materials
            */
        }

        public static bool shouldRun(){
            return modEnabled.Value && PolyTechMain.modEnabled.Value;
        }

        public void Start(){
            string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            bungee = LoadPNG(Path.Combine(basePath, resourceFolder, "bungee.png"));
            bungee_selected = LoadPNG(Path.Combine(basePath, resourceFolder, "bungee_selected.png"));
        }

        public void Update(){
            if (bungeeKeyBind.Value.IsDown() && shouldRun()){
                if (Budget.m_BungieRopeBudget == 0)
	            {
	            	InterfaceAudio.PlayErrorBeep();
	            	return;
	            }
	            GameUI.m_Instance.m_Materials.OnMaterial(BridgeMaterialType.BUNGINE_ROPE);
            }

            if (enableQueued && startupComplete){
                enableUI();
            }
            if (disableQueued && startupComplete){
                disableUI();
            }
        }


        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }
        public override void enableMod() 
        {
            modEnabled.Value = true;
            enableQueued = true;
            // set everything active
        }
        public override void disableMod() 
        {
            modEnabled.Value = false;
            disableQueued = true;
            // hide all the added ui elements
        }

        public void enableUI(){
            instance.m_BungeeButton.gameObject.SetActive(true);
            instance.m_BungeeInputField.gameObject.SetActive(true);
            
            // fix bungee edges
            foreach (BridgeEdge edge in BridgeEdges.m_Edges){
                if (edge.m_Material.m_MaterialType == BridgeMaterialType.BUNGINE_ROPE){
                    addBungeeSpring(edge);
                }
            }

            enableQueued = false;
        }
        public void disableUI(){
            instance.m_BungeeButton.gameObject.SetActive(false);
            instance.m_BungeeInputField.gameObject.SetActive(false);

            // make bungee materials not work
            foreach (BridgeEdge edge in BridgeEdges.m_Edges){
                if (edge.m_Material.m_MaterialType == BridgeMaterialType.BUNGINE_ROPE){
                    edge.m_SpringCoilVisualization = null;
                }
            }

            disableQueued = false;
        }

        public override string getSettings(){
            return $"{modEnabled.Value};";
        }
        public override void setSettings(string settings)
        {
            modEnabled.Value = settings.Split(';')[0] == "true";
        }

        public static Sprite LoadPNG(string filePath) {
 
            Texture2D tex = null;
            byte[] fileData;
            if (File.Exists(filePath))     {
                fileData = File.ReadAllBytes(filePath);
                tex = new Texture2D(2,2);
                tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
            }
            else {
                instance.Logger.LogWarning($"file {filePath} was not found.");
            }
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2());
            //instance.Logger.LogInfo($"{sprite.rect}");
            return sprite;
            //return tex;
        }

        [HarmonyPatch(typeof(GameUI), "StartManual")]
        public static class StartPatch {
            public static void Postfix(){
                string materialBar = "NonRoadGroup";
                if (!GameObject.Find(materialBar)) {
                    instance.Logger.LogInfo("'NonRoadGroup' not found, using 'MaterialBar'");
                    materialBar = "MaterialBar";
                }
                GameObject springButton = GameObject.Find($"GameUI/Panel_BottomBar/Panel_Materials/{materialBar}/Spring");
                GameObject cableButton = GameObject.Find($"GameUI/Panel_BottomBar/Panel_Materials/{materialBar}/Cable");

                GameObject bungeeObject = GameObject.Instantiate(springButton);
                bungeeObject.name = "Bungee_Rope";
                GameObject bungee_button = GameObject.Find("Bungee_Rope/Button_Spring");
                bungee_button.name = "Button_Bungee";
                bungee_button.transform.SetParent(bungeeObject.transform);

                GameObject target = GameObject.Find(materialBar);
                //instance.Logger.LogInfo($"target is null? {target == null}");
                if (target != null) bungeeObject.transform.SetParent(target.transform);
                
                //bungee_button.AddComponent<RectTransform>();
                CanvasRenderer canvasRenderer = bungee_button.GetComponent<CanvasRenderer>();
                Image img = bungee_button.GetComponent<Image>();
                img.sprite = instance.bungee_selected;
                //img.rectTransform.pivot = new Vector2(0,1);
                
                RectTransform baseTransform = bungeeObject.GetComponent<RectTransform>();
                
                baseTransform.anchoredPosition = new Vector2(302, -50);
                //img.rectTransform.position = new Vector3(550, 50, 0);
                
                baseTransform.anchoredPosition = new Vector2(302, -50);
                //instance.Logger.LogInfo(baseTransform.anchoredPosition);
                //instance.Logger.LogInfo(baseTransform.position);
                baseTransform.sizeDelta = new Vector2(50, 50);
                baseTransform.localScale = new Vector3(1,1,1);
                baseTransform.pivot = new Vector2(0, 0.5f);
                //instance.Logger.LogInfo("set size");
                
                //img.rectTransform.position = new Vector3(springTransform.position.x + 50f, springTransform.position.y, springTransform.position.z);
                GameObject.Destroy(bungee_button.GetComponent<TwoStateButton>());
                GameObject materialLimit = GameObject.Find($"GameUI/Panel_BottomBar/Panel_Materials/{materialBar}/Bungee_Rope/MaterialLimit");
                RectTransform materialLimitT = materialLimit.GetComponent<RectTransform>();
                materialLimitT.anchoredPosition = new Vector2(0, -5);
                MaterialLimit ml = materialLimit.GetComponent<MaterialLimit>();
                ml.gameObject.SetActive(false);
                instance.m_BungeeMaterialLimit = ml;

                GameObject DuckImageGameObject = GameObject.Find($"GameUI/Panel_BottomBar/Panel_Materials/{materialBar}/Bungee_Rope/Button_Bungee/DuckImage");
                Image DuckImage = DuckImageGameObject.GetComponent<Image>();
                DuckImage.sprite = instance.bungee;

                TwoStateButton btn = bungee_button.AddComponent<TwoStateButton>();
                btn.m_Image = img;
                btn.m_DuckImage = DuckImage;
                btn.m_SpriteOn = img.sprite;
                btn.m_SpriteOff = instance.bungee;
                btn.m_StartState = ButtonState.OFF;
                instance.click = new UnityEvent();
                instance.click.AddListener(instance.OnBungee);
                btn.m_PointerDownEvent = instance.click;
                btn.TurnOff();
                instance.m_BungeeButton = btn;
                instance.Logger.LogInfo("^ Ignore this, it was because an update frame happened before the images were set.");
                instance.Logger.LogInfo("Created material button.");

                // sandbox material budget

                GameObject resourceContainer = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/Panel_SandboxResources/ResourcesVerticalLayout/Resources");
                VerticalLayoutGroup group = resourceContainer.GetComponent<VerticalLayoutGroup>();
                GameObject start = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/Panel_SandboxResources/ResourcesVerticalLayout/Resources/Spring");
                GameObject bungeeResource = GameObject.Instantiate(start);
                bungeeResource.name = "Bungee Rope";
                bungeeResource.transform.SetParent(resourceContainer.transform);
                
                GameObject textObject = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/Panel_SandboxResources/ResourcesVerticalLayout/Resources/Bungee Rope/Text");
                TMPro.TextMeshProUGUI text = textObject.GetComponent<TMPro.TextMeshProUGUI>();

                GameObject.Destroy(textObject.GetComponent<I2.Loc.Localize>());
                text.text = "Bungine Rope";

                instance.m_BungeeInputField = bungeeResource.GetComponent<SandboxInputField>();
                bungeeResource.transform.localScale = new Vector3(1,1,1);
                instance.m_BungeeInputField.ChangeToFormat(SandboxInputFieldFormat.BUNGIE_ROPE_BUDGET);
                
                RectTransform panelTransform = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/Panel_SandboxResources").GetComponent<RectTransform>();
                panelTransform.sizeDelta = new Vector2(300, 400);

                instance.Logger.LogInfo("Created sandbox material input.");

            }
        }

        [HarmonyPatch(typeof(GameManager), "StartManual")]
        public static class GameStart_Patch {
            public static void Postfix(){
                // tooltip
                MaterialToolTipText toolTip = instance.m_BungeeButton.gameObject.GetComponent<MaterialToolTipText>();
                instance.m_BungeeToolTip = toolTip;
                BridgeMaterial bungeeRope = BridgeMaterials.GetMaterial(BridgeMaterialType.BUNGINE_ROPE);
                toolTip.m_Material = bungeeRope;
                bungeeRope.m_PricePerMeter = 375f;
                
                instance.startupComplete = true;

                instance.Logger.LogInfo("Created tooltip.");
            }
        }

        // Panel_Materials patches

        [HarmonyPatch(typeof(Panel_Materials), "GetMaterialButton")]
        public static class GetMaterialButton_Patch {
            public static bool Prefix(BridgeMaterialType materialType, ref TwoStateButton __result, Panel_Materials __instance){
                if (!shouldRun()) return true;
                switch (materialType)
	                {
	                case BridgeMaterialType.ROAD:
	                	__result = __instance.m_RoadButton;
                        return false;
	                case BridgeMaterialType.REINFORCED_ROAD:
	                	__result = __instance.m_ReinforcedRoadButton;
                        return false;
	                case BridgeMaterialType.WOOD:
	                	__result = __instance.m_WoodButton;
                        return false;
	                case BridgeMaterialType.STEEL:
	                	__result = __instance.m_SteelButton;
                        return false;
	                case BridgeMaterialType.HYDRAULICS:
	                	__result = __instance.m_HydraulicsButton;
                        return false;
	                case BridgeMaterialType.ROPE:
	                	__result = __instance.m_RopeButton;
                        return false;
	                case BridgeMaterialType.CABLE:
	                	__result = __instance.m_CableButton;
                        return false;
	                case BridgeMaterialType.SPRING:
	                	__result = __instance.m_SpringButton;
                        return false;
                    case BridgeMaterialType.BUNGINE_ROPE:
                        __result = instance.m_BungeeButton;
                        return false;
	            }
	            Debug.LogWarningFormat("Unexpected materialType in PanelMaterials.GetMaterialButton: {0}", new object[]
	            {
	            	materialType.ToString()
	            });
	            __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "OnLayoutLoaded")]
        public static class OnLayoutLoaded_Patch {
            public static void Postfix(Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                MethodInfo InitSlot = AccessTools.Method(typeof(Panel_Materials), "InitSlot");
                InitSlot.Invoke(
                    __instance, 
                    new object[] { 
                        instance.m_BungeeButton, 
                        instance.m_BungeeMaterialLimit,
                        Budget.m_BungieRopeBudget
                    }
                );
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "RefreshLimits")]
        public static class RefreshLimits_Patch {
            public static void Postfix(Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                instance.m_BungeeMaterialLimit.gameObject.SetActive(
                    Budget.m_BungieRopeBudget != Budget.UNLIMITED_MATERIAL_BUDGET &&
                    Budget.m_BungieRopeBudget != 0
                );
                instance.m_BungeeButton.transform.parent.gameObject.SetActive(Budget.m_BungieRopeBudget != 0);
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "SetMaterialIconsAlpha")]
        public static class SetMaterialIconsAlpha_Patch {
            public static void Postfix(Panel_Materials __instance, float ___DISABLED_ALPHA){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                instance.m_BungeeButton.SetAlpha((Budget.m_BungieRopeBudget > 0) ? 1f : ___DISABLED_ALPHA);
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "TurnOffSelectedMaterial")]
        public static class TurnOffSelectedMaterial_Patch {
            public static void Postfix(BridgeMaterialType material, Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                if (material == BridgeMaterialType.BUNGINE_ROPE){
                    instance.m_BungeeButton.TurnOff();
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "TurnOnSelectedMaterial")]
        public static class TurnOnSelectedMaterial_Patch {
            public static void Postfix(BridgeMaterialType material, Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                if (material == BridgeMaterialType.BUNGINE_ROPE){
                    instance.m_BungeeButton.TurnOn();
                }
            }
        }
        
        [HarmonyPatch(typeof(Panel_Materials), "UpdateMaterialLimits")]
        public static class UpdateMaterialLimits_Patch {
            public static void Postfix(Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                instance.m_BungeeMaterialLimit.Set(Budget.m_BungieRopeLeft);
            }
        }

        [HarmonyPatch(typeof(Panel_Materials), "UpdateTraceToolDucking")]
        public static class UpdateTraceToolDucking_Patch {
            public static void Postfix(Panel_Materials __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                TwoStateButton button = instance.m_BungeeButton;
                if (button.IsOn() && BridgeTrace.IsTracingActive())
	            {
	            	button.Duck();
	            	return;
	            }
	            button.UnDuck();
            }
        }

        [HarmonyPatch(typeof(SandboxInputField), "SetCallbacks", new Type[] { typeof(SandboxInputFieldFormat) })]
        public static class SetCallbacks_Patch {
            public static void Postfix(SandboxInputFieldFormat format, SandboxInputField __instance){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                if (format == SandboxInputFieldFormat.BUNGIE_ROPE_BUDGET){
                    MethodInfo SetCallbacks = AccessTools.Method(
                        typeof(SandboxInputField), 
                        "SetCallbacks", 
                        new Type[] { 
                            typeof(SandboxInputField.AddDeltaDelegate), 
                            typeof(SandboxInputField.SetDelegate), 
                            typeof(SandboxInputField.RestoreDelegate) 
                        }
                    );

                    SetCallbacks.Invoke(
                        __instance, 
                        new object[] {
                            new SandboxInputField.AddDeltaDelegate(instance.AddBungeeBudget),
                            new SandboxInputField.SetDelegate(instance.SetBungeeBudget),
                            new SandboxInputField.RestoreDelegate(instance.RestoreBungeeBudget)
                        }
                    );
                }
            }
        } 

        [HarmonyPatch(typeof(Panel_SandboxResources), "RefreshInputFields")]
        public static class RefreshInputFields_Patch {
            public static void Postfix(){
                if (!shouldRun() || instance.m_BungeeButton == null) return;
                instance.m_BungeeInputField.m_InputField.text = Utils.FormatMaterialBudget(Budget.m_BungieRopeBudget);
            }
        }

        [HarmonyPatch(typeof(MaterialToolTipText), "GetText")]
        public static class GetText_Patch {
            public static bool Prefix(MaterialToolTipText __instance, ref string __result){
                if (!shouldRun() || instance.m_BungeeButton == null) return true;
                if (__instance.m_Material == null) return true;
                if (__instance.m_Material.m_MaterialType == BridgeMaterialType.BUNGINE_ROPE){
                    string text = $"Bungee Rope\n(Hotkey: {bungeeKeyBind.Value})";
                    text += string.Format("\n${0}/m", __instance.m_Material.m_PricePerMeter);
                    __result = text;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(BridgeAudio), "PlayCreateEdge")]
        public static class PlayCreateEdge_Patch {
            public static void Postfix(BridgeMaterialType materialType){
                if (materialType == BridgeMaterialType.BUNGINE_ROPE){
                    MasterAudio.PlaySoundAndForget("ui_build_cable_place", 1f, null, 0f, null, null);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeAudio), "PlayMaterialSelect")]
        public static class PlayMaterialSelect_Patch {
            public static void Postfix(BridgeMaterialType materialType){
                if (materialType == BridgeMaterialType.BUNGINE_ROPE){
                    MasterAudio.PlaySoundAndForget("ui_build_cable_select", 1f, null, 0f, null, null);
                }
            }
        }


        public void AddBungeeBudget(GameObject go, float deltaBudget){
            Budget.m_BungieRopeBudget += Mathf.RoundToInt(deltaBudget);
		    if (Budget.m_BungieRopeBudget < 0)
		    {
		    	Budget.m_BungieRopeBudget = Budget.UNLIMITED_MATERIAL_BUDGET;
		    }
		    if (Budget.m_BungieRopeBudget > Budget.UNLIMITED_MATERIAL_BUDGET)
		    {
		    	Budget.m_BungieRopeBudget = 0;
		    }
		    SetBungeeBudget(null, (float)Budget.m_BungieRopeBudget);
        }

        public void SetBungeeBudget(GameObject go, float budget){
            if (budget < 0f)
		    {
		    	return;
		    }
		    Budget.m_BungieRopeBudget = Mathf.Clamp(Mathf.RoundToInt(budget), 0, Budget.UNLIMITED_MATERIAL_BUDGET);
		    instance.m_BungeeInputField.m_InputField.text = Utils.FormatMaterialBudget(Budget.m_BungieRopeBudget);
		    SandboxUndo.SnapShot();
        }

        public void RestoreBungeeBudget(GameObject go){
            instance.m_BungeeInputField.m_InputField.text = Utils.FormatMaterialBudget(Budget.m_BungieRopeBudget);
        }

        public void OnBungee(){
            GameUI.m_Instance.m_Materials.OnMaterial(BridgeMaterialType.BUNGINE_ROPE);
	        BridgeJointPlacement.CancelSelection();
        }

        public static void addBungeeSpring(BridgeEdge edge){
            BridgeSpring s = new BridgeSpring();
            s.m_FreeLengthOverrideMultiplier = 1f;
            edge.m_SpringCoilVisualization = s;
        }

        [HarmonyPatch(typeof(BridgeEdges), "CreateEdge")]
        public static class CreateEdgePatch {
            public static void Postfix(
                BridgeJoint jointA, 
                BridgeJoint jointB, 
                BridgeMaterialType materialType, 
                ref BridgeEdge __result,
                PolyPhysics.Edge physicsEdge_onlyUsedWhenBreakingEdgesInSimulation = null
            ){
                if (!shouldRun()) return;
                if (materialType == BridgeMaterialType.BUNGINE_ROPE){
                    addBungeeSpring(__result);
                }
            }
        }

        [HarmonyPatch(typeof(PolyPhysics.SpringAudioListener), "TriggerSpringAudioEvents")]
        public static class TriggerSpringAudioEventsPatch {
            public static bool Prefix(PolyPhysics.SpringAudioListener __instance, FastList<SpringData> ___springDatas){
                int progress = 0;
                try {
                    progress = 1;
                    //if (!shouldRun()) return true;
                    if (___springDatas == null) return false;
                    float fixedDeltaTime = Time.fixedDeltaTime;
	                float b = 0f;
                    progress = 2;
	                if (0 < ___springDatas.Count)
	                {
                        progress = 3;
                        if (___springDatas[0].edge == null || ___springDatas[0].edge?.world?.settings?.deltaTimeForVelocityEdge == null) return false;
                        progress = 4;
                        float deltaTimeForVelocityEdge = ___springDatas[0].edge.world.settings.deltaTimeForVelocityEdge;
                        if (1E-06f < deltaTimeForVelocityEdge)
	                	{
	                		b = 1f / deltaTimeForVelocityEdge;
	                	}
                        progress = 5;
	                }
	                for (int i = 0; i < ___springDatas.Count; i++)
	                {
	                	ref SpringData ptr = ref ___springDatas.array[i];
                        progress = 6;
                        //Debug.Log(ptr.edge?.material == null);
                        //if (ptr.edge.material.isRope && ptr.edge.material.isSpring) continue;
	                	ptr.timeSinceCompressionLastTriggered += fixedDeltaTime;
	                	ptr.timeSinceExpansionLastTriggered += fixedDeltaTime;
	                	progress = 7;
                        Vec2 a = ptr.edge.node1.solverNode.vel - ptr.edge.node0.solverNode.vel;
	                	a *= b;
	                	Vec2 vec = ptr.edge.node1.solverNode.pos - ptr.edge.node0.solverNode.pos;
	                	vec.Normalize();
                        progress = 8;
	                	float num = Vec2.Dot(a, vec);
	                	if (__instance.ExpandVelocityThresholdToTriggerSound < num && __instance.soundCooldownTime <= ptr.timeSinceExpansionLastTriggered)
	                	{
	                		ptr.timeSinceExpansionLastTriggered = 0f;
	                		Vec2 vec2 = 0.5f * (ptr.edge.node1.solverNode.pos + ptr.edge.node0.solverNode.pos);
	                		if (num > 4f)
	                		{
	                			MasterAudio.PlaySound3DAtVector3AndForget(__instance.expandBig, vec2, 1f, null, 0f, null, null);
	                		}
	                		else
	                		{
	                			MasterAudio.PlaySound3DAtVector3AndForget(__instance.expandSmall, vec2, 1f, null, 0f, null, null);
	                		}
	                		if (__instance.debugLogSounds)
	                		{
	                			Debug.Log(string.Concat(new object[]
	                			{
	                				"Spring Expansion at ",
	                				vec2,
	                				", speed: ",
	                				num
	                			}));
	                		}
                            progress = 9;
	                	}
	                	else if (num < __instance.CompressVelocityThresholdToTriggerSound && __instance.soundCooldownTime <= ptr.timeSinceCompressionLastTriggered)
	                	{
	                		ptr.timeSinceCompressionLastTriggered = 0f;
	                		Vec2 vec3 = 0.5f * (ptr.edge.node1.solverNode.pos + ptr.edge.node0.solverNode.pos);
	                		if (num < -4.5f)
	                		{
	                			MasterAudio.PlaySound3DAtVector3AndForget(__instance.compressBig, vec3, 1f, null, 0f, null, null);
	                		}
	                		else
	                		{
	                			MasterAudio.PlaySound3DAtVector3AndForget(__instance.compressSmall, vec3, 1f, null, 0f, null, null);
	                		}
	                		if (__instance.debugLogSounds)
	                		{
	                			Debug.Log(string.Concat(new object[]
	                			{
	                				"Spring Compression at ",
	                				vec3,
	                				", speed: ",
	                				num
	                			}));
	                		}
	                	}
                        progress = 10;
	                }
                }
                catch (Exception e){
                    // this function seems to cause null reference exceptions sometimes. 
                    // no idea if I fixed it so catch errors just in case.
                    instance.Logger.LogWarning($"Caught exception in PolyPhysics.SpringAudioListener::TriggerSpringAudioEvents {e.Message}");
                    instance.Logger.LogInfo($"point: {progress}");
                }
                return false;
            }
        }
    
    }
}