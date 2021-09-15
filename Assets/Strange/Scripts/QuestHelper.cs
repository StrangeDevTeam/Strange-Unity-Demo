﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestHelper : MonoBehaviour
{
    public static QuestHelper singleton;

    public float listingHeight = 0;

    [SerializeField]
    private Transform _content;
    [SerializeField]
    private QuestHelperListing _listing;
    [SerializeField]
    private Text questTitle;


    List<QuestHelperListing> listings = new List<QuestHelperListing>();
    // Start is called before the first frame update
    void Start()
    {
        singleton = this;
    }

    int i = 0;
    // Update is called once per frame
    void Update()
    {
        if (StrangeQuestSystem.trackedQuest == null)
            this.gameObject.transform.GetChild(0).gameObject.SetActive(false); // you must change the active state of the child,
        else                                                                   // if you change the active state of this.gameObject
            this.gameObject.transform.GetChild(0).gameObject.SetActive(true);  // then this script will stop running and the quest helper will never appear again
    }

    public void UpdateGUI()
    {
        if (StrangeQuestSystem.trackedQuest.complete)
            questTitle.text = "[COMPLETE] "+StrangeQuestSystem.trackedQuest.title;
        else 
            questTitle.text = StrangeQuestSystem.trackedQuest.title;

        // delete previous listings
        foreach(QuestHelperListing qhl in listings)
        {
            Destroy(qhl.gameObject);
        }
        listings.Clear();
        // create new listings
        int listingNumber = 0;
        foreach(QuestObjective qo in StrangeQuestSystem.trackedQuest.objectives)
        {
            QuestHelperListing temp = Instantiate(_listing,_content);
            temp.GetComponent<RectTransform>().anchoredPosition += new Vector2(0,-listingHeight * listingNumber);
            temp.objective = qo;
            listingNumber++;
            listings.Add(temp);
        }
    }
}
