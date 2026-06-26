using System.IO;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using GHPC;
using GHPC.Player;
using GHPC.State;
using GHPC.UI.Hud;
using GHPC.Weapons;
using GHPC.Vehicle;
using MelonLoader;
using PactAzimuthIndicatorMod;
using HarmonyLib;

[assembly: MelonInfo(typeof(PactAzimuthIndicator), "Pact Azimuth Indicator", "1.0.0", "Bluehawk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace PactAzimuthIndicatorMod
{

    public class AzimuthText : MonoBehaviour
    {
        public Text _weaponText;
        public WeaponHud _weaponHUD;
        public PlayerInput playerInput;
        public NwhChassis _chassis;
        public AimablePlatform _aimablePlatform;
        public void Update()
        {
            if (_weaponText != null && _aimablePlatform != null)
            {
                string temp;
                var _sbRef = AccessTools.FieldRefAccess<WeaponHud, StringBuilder>("_sb");
                StringBuilder weapSB = _sbRef(_weaponHUD);
                temp = weapSB.ToString();

                
                float turretHeading = _aimablePlatform.LocalRotation.eulerAngles.y;
                int mils = (int)(turretHeading * 16.667f);
                if (mils > 3000) mils -= 3000; else mils += 3000;                
                string milsString = mils.ToString();                
                if (mils > 100 && mils < 1000) milsString = milsString.Insert(0, "0");
                if (mils < 100) milsString = milsString.Insert(0, "00");
                milsString = milsString.Insert(2, "-");
                _weaponText.text = temp + "\n" + milsString + " mils";                
            }
        }

        public void LateUpdate()
        {
            if (playerInput.CurrentPlayerChassis != null) _chassis = playerInput.CurrentPlayerChassis as NwhChassis;
            WeaponsManager wepMan = (playerInput.CurrentPlayerUnit != null) ? playerInput.CurrentPlayerUnit.WeaponsManager : null;
            if (wepMan != null) _aimablePlatform = wepMan.Weapons[0].FCS.Mounts[0];
        }

    }
    public class PactAzimuthIndicator : MelonMod
    {
        public static GameObject gameManager;
        public static Text weaponText;

        public static MelonPreferences_Entry<bool> clock_sprite;
        public static MelonPreferences_Entry<bool> mils_readout;
        public static MelonPreferences_Entry<bool> pact_only;

        public override void OnInitializeMelon()
        {
            MelonPreferences_Category cfg = MelonPreferences.CreateCategory("Pact Azimuth Indicator");
            clock_sprite = cfg.CreateEntry<bool>("Modify the hull sprite", true);
            clock_sprite.Comment = "Replaces default hull-turret diagram with a clock face in 500 mil intervals";
            mils_readout = cfg.CreateEntry<bool>("Adds milliradian readout to HUD", true);
            mils_readout.Comment = "Adds the precise mil heading of the turret to the HUD, in Soviet format (30-00 is straight ahead)";
            pact_only = cfg.CreateEntry<bool>("Pact vehicles only", false);
            pact_only.Comment = "Modifies the HUD only when playing Warsaw Pact vehicles";
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
            Vehicle playerVic = GameObject.FindObjectOfType<PlayerInput>().CurrentPlayerDamageStatus as Vehicle;
            string playerVicShort = playerVic.UniqueName.Substring(0,3);
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
                GameObject hull_go = gameManager.transform.Find("UIHUDCanvas/weapons text/azimuth HUD/hull").gameObject;
                Image hull_img = hull_go.GetComponent<Image>();
                hull_img.sprite = newSprite;
            }

            if (gameManager.transform.Find("UIHUDCanvas/weapons text").gameObject.GetComponent<AzimuthText>() == null) {
                if (mils_readout.Value)
                {
                    AzimuthText azimuthText = gameManager.transform.Find("UIHUDCanvas/weapons text").gameObject.AddComponent<AzimuthText>();
                    azimuthText._weaponText = gameManager.transform.Find("UIHUDCanvas/weapons text").gameObject.GetComponent<Text>();
                    azimuthText._weaponHUD = gameManager.transform.Find("UIHUDCanvas/weapons text").gameObject.GetComponent<WeaponHud>();
                    azimuthText.playerInput = GameObject.FindObjectOfType<PlayerInput>();
                }
            }
            
            yield break;
        }
    }
}
