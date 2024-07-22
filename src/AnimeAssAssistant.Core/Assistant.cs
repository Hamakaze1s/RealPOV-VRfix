﻿using ChaCustom;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AnimeAssAssistant
{
    internal class Assistant : MonoBehaviour
    {
        private string currentCharacter;
        private List<string> loadedCharacters = new List<string>();

        private void Update()
        {
            if(AAA.EnableAAA)
            {
                if(AAA.HotkeyNext.Value.IsDown())
                    LoadNextChara();
                else if(AAA.HotkeyPrev.Value.IsDown())
                    LoadPrevChara();
                else if(AAA.HotkeyKill.Value.IsDown())
                    RecycleCurrentChara();
                else if(AAA.HotkeySave.Value.IsDown())
                    SaveCurrentChara();
            }
        }

        public void ClearLoadedCharas()
        {
            currentCharacter = null;
            loadedCharacters.Clear();
        }

        private void LoadRandomChara()
        {
            if(string.IsNullOrEmpty(AAA.SearchFolder.Value))
            {
                Log.Message("Search folder has not been set, please set it in ConfigManager");
                return;
            }

            var files = Directory.GetFiles(AAA.SearchFolder.Value, "*.png");
            if(files.Length == 0)
            {
                Log.Message("Search folder is empty");
                return;
            }

            files = files.Except(loadedCharacters).ToArray();
            Log.Debug($"Found {files.Length} new PNG files in {AAA.SearchFolder.Value}");

            if(files.Length > 0)
            {
                var path = files[UnityEngine.Random.Range(0, files.Length - 1)];
                loadedCharacters.Add(path);
                LoadChara(path);
            }
            else
            {
                LoadChara(loadedCharacters[0]);
            }

        }

        private void RecycleCurrentChara()
        {
            if(!string.IsNullOrEmpty(currentCharacter))
            {
                RecycleBinUtil.MoveToRecycleBin(currentCharacter);
                loadedCharacters.Remove(currentCharacter);
                Log.Info($"{currentCharacter} moved to the recycle bin.");
                LoadRandomChara();
            }
        }

        private void SaveCurrentChara()
        {
            if(string.IsNullOrEmpty(AAA.SaveFolder.Value))
            {
                Log.Message("Save folder has not been set, please set it in ConfigManager");
                return;
            }

            if(!string.IsNullOrEmpty(currentCharacter))
            {
                var dest = Path.Combine(AAA.SaveFolder.Value, Path.GetFileName(currentCharacter));
                File.Move(currentCharacter, dest);
                loadedCharacters.Remove(currentCharacter);
                Log.Info($"{currentCharacter} moved to save folder.");
                LoadRandomChara();
            }
        }

        private void LoadNextChara()
        {
            var index = loadedCharacters.IndexOf(currentCharacter);
            if(index == -1 || index == loadedCharacters.Count - 1)
                LoadRandomChara();
            else
                LoadChara(loadedCharacters[index + 1]);
        }

        private void LoadPrevChara()
        {
            var index = loadedCharacters.IndexOf(currentCharacter);
            if(index != -1 && index != 0)
                LoadChara(loadedCharacters[index - 1]);
        }

        private void LoadChara(string path)
        {
            currentCharacter = path;

            var cfw = GameObject.FindObjectsOfType<CustomFileWindow>().FirstOrDefault(x => x.fwType == CustomFileWindow.FileWindowType.CharaLoad);
            var loadFace = true;
            var loadBody = true;
            var loadHair = true;
            var parameter = true;
            var loadCoord = true;

            if(cfw)
            {
                loadFace = cfw.tglChaLoadFace && cfw.tglChaLoadFace.isOn;
                loadBody = cfw.tglChaLoadBody && cfw.tglChaLoadBody.isOn;
                loadHair = cfw.tglChaLoadHair && cfw.tglChaLoadHair.isOn;
                parameter = cfw.tglChaLoadParam && cfw.tglChaLoadParam.isOn;
                loadCoord = cfw.tglChaLoadCoorde && cfw.tglChaLoadCoorde.isOn;
            }

            var chaCtrl = CustomBase.Instance.chaCtrl;
            var originalSex = chaCtrl.sex;

            chaCtrl.chaFile.LoadFileLimited(path, chaCtrl.sex, loadFace, loadBody, loadHair, parameter, loadCoord);
            if(chaCtrl.chaFile.GetLastErrorCode() != 0)
                throw new Exception("LoadFileLimited failed");

            if(chaCtrl.chaFile.parameter.sex != originalSex)
            {
                chaCtrl.chaFile.parameter.sex = originalSex;
                Log.Message("Warning: The character's sex has been changed to match the editor mode.");
            }

            chaCtrl.ChangeCoordinateType();
            chaCtrl.Reload(!loadCoord, !loadFace && !loadCoord, !loadHair, !loadBody);
            CustomBase.Instance.updateCustomUI = true;
        }
    }
}
