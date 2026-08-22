using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GHPC;
using GHPC.Camera;
using GHPC.Player;
using GHPC.State;
using GHPC.UI.Hud;
using GHPC.Weapons;
using GHPC.Vehicle;
using MelonLoader;
using PactAzimuthIndicatorMod;
using HarmonyLib;

[assembly: MelonInfo(typeof(PactAzimuthIndicator), "Pact Azimuth Indicator", "1.2.1", "Bluehawk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace PactAzimuthIndicatorMod
{

    public class AzimuthText : MonoBehaviour
    {
        public Text _textBox;        
        public PlayerInput playerInput;
        public NwhChassis _chassis;
        public AimablePlatform _aimablePlatform;
        public RectTransform ownRect;
        public RectTransform anchor;
        public void Update()
        {
            if (_textBox != null && _aimablePlatform != null)
            { 
                float turretHeading = _aimablePlatform.LocalRotation.eulerAngles.y;
                int mils = (int)(turretHeading * 16.667f);
                if (mils > 3000) mils -= 3000; else mils += 3000;                
                string milsString = mils.ToString(); 
                if (mils < 10) milsString = milsString.Insert(0, "000"); 
                else if (mils < 100) milsString = milsString.Insert(0, "00");
                else if (mils < 1000) milsString = milsString.Insert(0, "0");
                milsString = milsString.Insert(2, "-");
                _textBox.text = milsString + " mils";                
            }
        }

        public void LateUpdate()
        {
            if (playerInput.CurrentPlayerChassis != null) _chassis = playerInput.CurrentPlayerChassis as NwhChassis;
            WeaponsManager wepMan = (playerInput.CurrentPlayerUnit != null) ? playerInput.CurrentPlayerUnit.WeaponsManager : null;
            if (wepMan != null) _aimablePlatform = wepMan.Weapons[0].FCS.Mounts[0];
            ownRect.position = new Vector3(anchor.position.x + anchor.rect.width, ownRect.position.y, ownRect.position.z);
        }       

    }

    public class ClockTargetManager : MonoBehaviour
    {
        public Compass _compass;
        public GameObject _targetMarker;
        public PlayerInput playerInput;
        public NwhChassis _vehicle;

        public void LateUpdate()
        {
            var compPairRef = AccessTools.FieldRefAccess<Compass, List<Compass.CompassPointImagePair>>("_compassPairs");
            List<Compass.CompassPointImagePair> compassPairs = compPairRef(_compass);            
            if (compassPairs.Count == 0) {
                _targetMarker.SetActive(false);
                return;
            }
            
            _targetMarker.SetActive(true);
            compassPairs[0].Image.color = new Color(0f, 0f, 0f, 0f);
            _vehicle = playerInput.CurrentPlayerChassis as NwhChassis;
            Vector3 targetPosition = compassPairs[0].CompassPoint.Position;
            float angle = Vector2.SignedAngle(new Vector2(targetPosition.x, targetPosition.z) - new Vector2(_vehicle.transform.position.x, _vehicle.transform.position.z), 
                new Vector2(_vehicle.transform.forward.x, _vehicle.transform.forward.z));            
            
            _targetMarker.transform.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, -angle);
        }
    }
    public class PactAzimuthIndicator : MelonMod
    {
        public static GameObject gameManager;
        public static Text weaponText;

        public static MelonPreferences_Entry<bool> clock_sprite;
        public static MelonPreferences_Entry<bool> larger_clock;
        public static MelonPreferences_Entry<bool> mils_readout;
        public static MelonPreferences_Entry<bool> pact_only;
        public static MelonPreferences_Entry<bool> clock_targets;

        public override void OnInitializeMelon()
        {
            MelonPreferences_Category cfg = MelonPreferences.CreateCategory("Pact Azimuth Indicator");
            clock_sprite = cfg.CreateEntry<bool>("Modify the hull sprite", true);
            clock_sprite.Comment = "Replaces default hull-turret diagram with a clock face in 500 mil intervals";
            larger_clock = cfg.CreateEntry<bool>("Larger hull-turret diagram", false);
            larger_clock.Comment = "Increases the size of the diagram for readability";
            mils_readout = cfg.CreateEntry<bool>("Adds milliradian readout to HUD", true);
            mils_readout.Comment = "Adds the precise mil heading of the turret to the HUD, in Soviet format (30-00 is straight ahead)";
            pact_only = cfg.CreateEntry<bool>("Pact vehicles only", false);
            pact_only.Comment = "Modifies the HUD only when playing Warsaw Pact vehicles";
            clock_targets = cfg.CreateEntry<bool>("Mark targets on azimuth indicator", true);
            clock_targets.Comment = "Designated targets will appear on the azimuth indicator instead of on the compass";
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "MainMenu2_Scene" || sceneName == "t64_menu" || sceneName == "MainMenu2-1_Scene") return;            

            gameManager = GameObject.Find("_APP_GHPC_");
            if (gameManager == null) return;

            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(TurretClock), GameStatePriority.Medium);
        }
        public IEnumerator TurretClock(GameState _)
        {
            Vehicle playerVic = Object.FindObjectOfType<PlayerInput>().CurrentPlayerDamageStatus as Vehicle;
            string playerVicShort;            
            if (playerVic.UniqueName.Length < 3) playerVicShort = playerVic.UniqueName + "    "; 
            else playerVicShort = playerVic.UniqueName.Substring(0, 3);
            bool warsawPact;
            switch (playerVicShort)
            {
                case ("UAZ"):
                case ("URA"):
                case ("BRD"):
                case ("BMP"):
                case ("BTR"):
                case ("PT7"):
                case ("T34"):
                case ("T54"):
                case ("T55"):
                case ("T62"):
                case ("T64"):
                case ("T72"):
                case ("T80"):
                    warsawPact = true;
                    break;
                default:
                    warsawPact = false;
                    break;
            }
            if (playerVic.UniqueName == "STATIC_SPG9" || playerVic.UniqueName == "STATIC_9K111") warsawPact = true;
            if (pact_only.Value && warsawPact == false) yield break;

            if (clock_sprite.Value) {
                Texture2D newClock = new Texture2D(512, 512);
                byte[] newClock_data = File.ReadAllBytes("Mods/PactAzimuthIndicator/azimuth_hull.png");
                newClock.LoadImage(newClock_data);
                Sprite newSprite = Sprite.Create(newClock, new Rect(0f, 0f, newClock.width, newClock.height), new Vector2(0.5f, 0.5f), 100f);
                GameObject azimuthHUD = gameManager.transform.Find("UIHUDCanvas/weapons text/azimuth HUD").gameObject;                
                Image hull_img = azimuthHUD.transform.Find("hull").GetComponent<Image>();
                hull_img.sprite = newSprite;                
                RectTransform turretRect = azimuthHUD.transform.Find("turret").GetComponent<RectTransform>();                
                turretRect.localScale = new Vector3(0.6f, 1f, 1f);

                if (clock_targets.Value)
                {
                    if (azimuthHUD.transform.Find("hull/target") == null) { 
                        GameObject target = new GameObject("target");
                        target.transform.parent = azimuthHUD.transform.Find("hull");
                        RectTransform target_rect = target.AddComponent<RectTransform>();
                        target_rect.anchoredPosition = new Vector2(0f, 0f);
                        target_rect.localScale = new Vector3(0.6f, 1f, 1f);
                        Image target_img = target.AddComponent<Image>();
                        target_img.sprite = azimuthHUD.transform.Find("turret").GetComponent<Image>().sprite;
                        target_img.color = new Color(1f, 0.2736f, 0f, 1f);

                        ClockTargetManager clockTargetManager = azimuthHUD.AddComponent<ClockTargetManager>();
                        clockTargetManager._targetMarker = target;
                        clockTargetManager._compass = Object.FindObjectOfType<Compass>();
                        clockTargetManager.playerInput = Object.FindObjectOfType<PlayerInput>();
                        target.SetActive(false);
                    }
                }

                if (larger_clock.Value) { 
                    azimuthHUD.GetComponent<RectTransform>().anchoredPosition = new Vector2(154.6f, 0f);
                    azimuthHUD.GetComponent<RectTransform>().localScale = new Vector3(1.4f, 1.4f, 0f);
                }
            }

            if (gameManager.transform.Find("UIHUDCanvas/pact mils text") == null) {                
                if (mils_readout.Value)
                {
                    GameObject milsText_go = new GameObject("pact mils text");
                    milsText_go.transform.parent = gameManager.transform.Find("UIHUDCanvas");                    
                    RectTransform rectT = milsText_go.gameObject.AddComponent<RectTransform>();
                    Text textBox = milsText_go.gameObject.AddComponent<Text>();
                    AzimuthText azimuthText = milsText_go.gameObject.AddComponent<AzimuthText>();                    
                    
                    rectT.anchoredPosition = new Vector2(335f, 89f);
                    rectT.anchorMax = new Vector2(0f, 0f);
                    rectT.anchorMin = new Vector2(0f, 0f);
                    rectT.sizeDelta = new Vector2(224.61f, 30f);
                    rectT.localScale = new Vector3(1f, 1f, 1f);
                    textBox.font = gameManager.transform.Find("UIHUDCanvas/drivetrain text").GetComponent<Text>().font;
                    textBox.fontSize = 14;
                    
                    azimuthText._textBox = textBox;                    
                    azimuthText.playerInput = Object.FindObjectOfType<PlayerInput>();
                    azimuthText.ownRect = rectT;
                    azimuthText.anchor = gameManager.transform.Find("UIHUDCanvas/weapons text/azimuth HUD").GetComponent<RectTransform>();

                    FreeCamExclusionList excList = gameManager.GetComponent<FreeCamExclusionList>();
                    int oldLength = excList.ExcludedItems.Length;                    
                    GameObject[] tempArray = new GameObject[oldLength + 1];                    
                    for (int i = 0; i < oldLength; i++)
                    {
                        tempArray[i] = excList.ExcludedItems[i];                        
                    }
                    tempArray[oldLength] = milsText_go;                    
                    excList.ExcludedItems = tempArray;
                }
            }
            
            yield break;
        }
    }
}
