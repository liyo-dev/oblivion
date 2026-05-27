using UnityEngine;
using UnityEngine.UI;

namespace NextPrefab
{
    public class NextPrefab : MonoBehaviour
    {
        #region Variables


        public GameObject[] m_PrefabList;

        public GameObject[] m_SceneEffectsList;

        private int m_CurrentIndex = 0;

        private GameObject m_CurrentSpawnedObj = null;


        public Text m_ParticleName;

        #endregion


        void Start()
        {
            DisableAllSceneEffects();

            ShowCurrent();
        }


        void Update()
        {
            if (Input.GetKeyUp(KeyCode.Z))
            {
                m_CurrentIndex--;
                ShowCurrent();
            }
            else if (Input.GetKeyUp(KeyCode.X))
            {
                m_CurrentIndex++;
                ShowCurrent();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                if (m_CurrentIndex < m_PrefabList.Length)
                    RespawnPrefab();
            }
        }


        #region Logic

        void ShowCurrent()
        {
            int totalCount = m_PrefabList.Length + m_SceneEffectsList.Length;

            if (m_CurrentIndex < 0)
                m_CurrentIndex = totalCount - 1;
            else if (m_CurrentIndex >= totalCount)
                m_CurrentIndex = 0;

            DisableAllSceneEffects();

            if (m_CurrentSpawnedObj != null)
            {
                Destroy(m_CurrentSpawnedObj);
                m_CurrentSpawnedObj = null;
            }

            if (m_CurrentIndex < m_PrefabList.Length)
            {
                GameObject prefab = m_PrefabList[m_CurrentIndex];
                m_ParticleName.text = prefab.name;

                m_CurrentSpawnedObj = Instantiate(prefab);
            }
            else
            {
                int sceneIndex = m_CurrentIndex - m_PrefabList.Length;

                GameObject sceneObj = m_SceneEffectsList[sceneIndex];
                m_ParticleName.text = "[Scene] " + sceneObj.name;

                sceneObj.SetActive(true);
            }
        }


        void RespawnPrefab()
        {
            if (m_CurrentSpawnedObj != null)
                Destroy(m_CurrentSpawnedObj);

            GameObject prefab = m_PrefabList[m_CurrentIndex];
            m_CurrentSpawnedObj = Instantiate(prefab);
        }


        void DisableAllSceneEffects()
        {
            foreach (var obj in m_SceneEffectsList)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }

        #endregion


        #region UI Buttons

        public void OnLeftArrowPressed()
        {
            m_CurrentIndex--;
            ShowCurrent();
        }

        public void OnRightArrowPressed()
        {
            m_CurrentIndex++;
            ShowCurrent();
        }

        #endregion
    }
}
