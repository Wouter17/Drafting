using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using Random = UnityEngine.Random;
// ReSharper disable ConvertIfStatementToNullCoalescingAssignment

public class Drafting : MonoBehaviour
{

	public new KMAudio audio;
	public KMBombInfo bomb;
	
	//keypad
	public KMSelectable[] keypad;
	public Renderer[] teams;
	public Renderer[] teamBlue;
	public Renderer[] teamRed;
	public Material[] teamMaterials;
	public Material[] championMaterials;
	
	//constants
	private readonly char[] _alphabet = Constants.Alphabet();
	private readonly string[] _teamsList = Constants.Teams();
	private readonly Champion[] _championsList = Constants.Champions();
	private Champion[][] _enemyRoleOrder = {Constants.Top(), Constants.Jungle(), Constants.Mid(), Constants.Bot(), Constants.Sup()};
	private const int PickDelay = 0; //Delay maximum of enemy picking champions normally 31

	//status
	private int _animationPlaying;
	private string _phase = "ban1";
	private Champion _correctAnswer;
	private readonly List<string> _teams = new List<string>();
	private List<Champion> _bannedChampions = new List<Champion>();
	private List<Champion> _selectableChampions = new List<Champion>();
	//TODO: find if _lockedChampions has a use or can be removed
	//private List<Champion> _lockedChampions = new List<Champion>();
	
	//logging
	private static int _moduleIdCounter = 1;
	private int _moduleId;
	private bool _moduleSolved;

	private void Awake()
	{
		_moduleId = _moduleIdCounter++;
		foreach (var key in keypad){
		    var pressedObject = key;
		    key.OnInteract += delegate { KeypadPress(pressedObject); return false; };
		}
	}

	private void Start()
	{
		Debug.LogFormat("[Drafting #{0}] Champions to choose from will be logged in reading order \nAll special characters have been removed from champion and team names \nCompared to the nth champion, the index is 1 lower (Aatrox is 0)",_moduleId);
		SetTeams();
		_enemyRoleOrder = _enemyRoleOrder.Shuffle();
		_bannedChampions = GenerateFirstBans();
		_selectableChampions = _championsList.Except(_bannedChampions).ToList();
		
		//TODO: Remove sound for final version
		audio.HandlePlaySoundAtTransform("gragas", transform);
	}
	
	private void KeypadPress(Component key){
	    if(_moduleSolved || _animationPlaying > 0){
	        return;
	    }
	    var keyMaterialName = key.GetComponent<MeshRenderer>().material.name;
	    var champion = EnumUtil.ParseEnum<Champion>(keyMaterialName.Substring(0, keyMaterialName.Length - 11));
	    Debug.LogFormat("[Drafting #{0}] Pressed {1}",_moduleId, champion);
	    
	    if(_phase != "ban1") audio.HandlePlaySoundAtTransform(champion.ToString(), transform);
	    
	    if (_phase.StartsWith("ban"))
	    {
		    if (_phase.EndsWith("1"))
		    {
			    _phase = "pick1";
			    _correctAnswer = GenerateFirstPick();
		    }
		    else if(_phase.EndsWith("2"))
		    {
			    _phase = "pick4";
		    }else throw new ArgumentException("invalid phase", _phase);
	    }
	    else if (_phase.StartsWith("pick"))
	    {
		    if (_correctAnswer != champion) HandleStrike(_correctAnswer, champion);
		    _selectableChampions.RemoveAll(x => x == champion);
		    
		    if (_phase.EndsWith("1"))
		    {
			    SetMaterialChampion(teamBlue[0], champion);
			    var delay = Random.Range(0, PickDelay);
			    StartCoroutine(GenerateChampion(0, _enemyRoleOrder[0].ToList(), delay));
			    StartCoroutine(GenerateChampion(1, _enemyRoleOrder[1].ToList(), delay + Random.Range(0, PickDelay)));
			    _phase = "pick2";
			    _correctAnswer = GenerateSecondPick();
		    }
		    else if(_phase.EndsWith("2"))
		    {
			    SetMaterialChampion(teamBlue[1], champion);
			    _phase = "pick3";
			    _correctAnswer = GenerateThirdPick();
		    }
		    else if(_phase.EndsWith("3"))
		    {
			    SetMaterialChampion(teamBlue[2], champion);
			    StartCoroutine(GenerateChampion(2, _enemyRoleOrder[2].ToList()));
			    //ban 2 happening here
			    _bannedChampions.AddRange(GenerateSecondBans());
			    _selectableChampions = _selectableChampions.Except(_bannedChampions).ToList();
			    //Generate enemy champion
			    StartCoroutine(GenerateChampion(3, _enemyRoleOrder[3].ToList()));
			    
			    _phase = "pick4";
			    _correctAnswer = GenerateFourthPick();
		    }
		    else if(_phase.EndsWith("4"))
		    {
			    SetMaterialChampion(teamBlue[3], champion);
			    _phase = "pick5";
		    }
		    else if(_phase.EndsWith("5"))
		    {
			   
			    _phase = "end";
			    _moduleSolved = true;
			    HandleSolved();
		    }
		    else throw new ArgumentException("invalid phase", _phase);
	    }
	    else throw new ArgumentException("Unknown variable for _phase", _phase);
	    
	    Debug.LogFormat("[Drafting #{0}] The phase is now: {1}", _moduleId, _phase);
	}

	/**
		generates the teams playing
	*/
	private void SetTeams()
	{
		var availableTeams = _teamsList;
		foreach (var teamSpace in teams)
		{
			var team = availableTeams[Random.Range(0, availableTeams.Length)];
			teamSpace.GetComponent<MeshRenderer>().material = teamMaterials[Array.IndexOf(_teamsList, team)];
			availableTeams = availableTeams.Where(x => x != team).ToArray();
			_teams.Add(team);
		}
		Debug.LogFormat("[Drafting #{0}] The teams are: {1} vs {2}",_moduleId,_teams[0],_teams[1]);
	}
	
	/**
		generates the first round of bans
	*/
	private List<Champion> GenerateFirstBans()
	{
		var screenCharactersIndices = GenerateUniqueRandomRange(0, _championsList.Length, 6);
		for (var i = 0; i < keypad.Length; i++)
		{
			SetMaterialChampion(keypad[i], _championsList[screenCharactersIndices[i]]);
		}
		LOG(string.Format("The champions displayed during ban1 phase are: {0} with indexes {1}", 
			string.Join(", ",screenCharactersIndices.Select(x=>_championsList[x].ToString()).ToArray()), 
			string.Join(", ", screenCharactersIndices.Select(x=>x.ToString()).ToArray()))
		);
		
		var serial = bomb.GetSerialNumber().ToLower();
		var bansByIndex = screenCharactersIndices.Select((value, index) =>
		{
			if (char.IsLetter(serial[index])) 
				return ((value + 1) * (Array.IndexOf(_alphabet, serial[index]) + 1)) % _championsList.Length;
			else return ((value + 1) * (int)char.GetNumericValue(serial[index])) % _championsList.Length;

		}).ToList();
		
		var notDuplicate = new List<int>();
		for (var i = bansByIndex.Count-1; i >= 0; i--)
		{
			while (notDuplicate.Contains(bansByIndex[i]))
			{
				bansByIndex[i] = (bansByIndex[i] + 1) % _championsList.Length;
			}
			notDuplicate.Add(bansByIndex[i]);
		}
		var bansByName = bansByIndex.Select(ban => _championsList[ban]).ToArray();
		LOG(string.Format("The first six bans are: {0} with indexes {1}",
			string.Join(", ", bansByName.Select(champ => champ.ToString()).ToArray()),
			string.Join(", ", bansByIndex.Select(x => x.ToString()).ToArray()))
		);
		return bansByName.ToList();
	}

	private IEnumerable<Champion> GenerateSecondBans()
	{
		var serial = bomb.GetSerialNumber().ToLower();
		var serialGroups = new List<string>()
			{serial.Substring(0, 2), serial.Substring(2, 2), serial.Substring(4, 2), serial};
		var serialValues = serialGroups.Select(serialGroup => {
			var value = 0;
			if (char.IsLetter(serialGroup[0]))
				value += Array.IndexOf(Constants.Alphabet(), serialGroup[0]);
			else
			{
				value += (int) char.GetNumericValue(serialGroup[0]);
			}
			if (char.IsLetter(serialGroup[1]))
				value += 36 * Array.IndexOf(Constants.Alphabet(), serialGroup[1]);
			else
			{
				value += 36 * (int) char.GetNumericValue(serialGroup[1]);
			}

			return (value + 1) % _championsList.Length ;
		}).ToList();

		var bannedChampions = new List<Champion>();
		for (var i = serialValues.Count-1; i >= 0; i--)
		{
			var champToBan =  _championsList[serialValues[i]];
			while (!_selectableChampions.Contains(champToBan) || bannedChampions.Contains(champToBan))
			{
				champToBan = _championsList[(Array.IndexOf(_championsList, champToBan) + 1) % _championsList.Length ];
			}
			
			bannedChampions.Add(champToBan);
		}
		LOG("The second four bans are: " + string.Join(", ", bannedChampions.Select(champion => champion.ToString()).ToArray()));
		
		return bannedChampions;
	}

	/**
		generator the first left side pick
	*/
	private Champion GenerateFirstPick()
	{
		var table = new []
		{
			new [] {Champion.yuumi, Champion.viego, Champion.alistar, Champion.nocturne, Champion.lucian},
			new [] {Champion.kalista, Champion.leesin, Champion.gwen, Champion.syndra, Champion.leblanc},
			new [] {Champion.tahmkench, Champion.thresh, Champion.rumble, Champion.xinzhao, Champion.ziggs},
			new [] {Champion.drmundo, Champion.akali, Champion.azir, Champion.lulu, Champion.jayce},
			new [] {Champion.zed, Champion.volibear, Champion.varus, Champion.ezreal, Champion.renekton},
		};
		int row;
		int col;
		switch (_teams[0])
		{
			case "astralis":
				row = bomb.GetOffIndicators().Count();
				col = bomb.GetPortPlateCount();
				break;
			case "excelesports":
				row = bomb.GetBatteryHolderCount();
				col = bomb.GetOffIndicators().Count();
				break;
			case "fnatic":
				row = bomb.GetOnIndicators().Count();
				col = bomb.GetBatteryHolderCount();
				break;
			case "g2esports":
				row = bomb.GetSerialNumberNumbers().Last() + bomb.GetSolvableModuleNames().Count;
				col = bomb.GetOnIndicators().Count();
				break;
			case "madlions":
				row = Array.IndexOf(_alphabet, char.ToLower(bomb.GetSerialNumberLetters().First()));
				col = bomb.GetSerialNumberNumbers().Last() + bomb.GetSolvableModuleNames().Count;
				break;
			case "misfitsgaming":
				row = bomb.GetOnIndicators().Count();
				col = Array.IndexOf(_alphabet, char.ToLower(bomb.GetSerialNumberLetters().First()));
				break;
			case "rogue":
				row = bomb.GetBatteryCount() + bomb.GetSerialNumberNumbers().Last();
				col = bomb.GetOnIndicators().Count();
				break;
			case "schalke04":
				row = bomb.GetSerialNumberNumbers().First();
				col = bomb.GetBatteryCount() + bomb.GetSerialNumberNumbers().Last();
				break;
			case "skgaming":
				row = bomb.GetSerialNumberNumbers().Last();
				col = bomb.GetSerialNumberNumbers().First();
				break;
			case "vitality":
				row = bomb.GetPortPlateCount();
				col = bomb.GetSerialNumberNumbers().Last();
				break;
			default: throw new ArgumentException("Invalid Team", _teams[0]);
		}
		row %= 5;
		col %= 5;
		var champion = table[row][col];
		LOG(string.Format("Phase 1: The correct row is {0} and the correct column is {1} which is {2}", row+1, col+1, champion));
		
		while (_bannedChampions.Contains(champion)) // check if the champion isn't banned
		{
			champion = _championsList[(Array.IndexOf(_championsList, champion) + 1)% _championsList.Length];
			LOG(string.Format("The previous champion was not available, the correct champion is now {0} ", champion));
		}
		
		var screenCharacters = GenerateUniqueRandomRange(0, _selectableChampions.Count, 6, new List<int>(){_selectableChampions.IndexOf(champion)});
		for (var i = 0; i < keypad.Length; i++)
		{
			SetMaterialChampion(keypad[i], _selectableChampions[screenCharacters[i]]);
		}
		SetMaterialChampion( keypad[Random.Range(0,keypad.Length)], champion);
		LOG(string.Format("The champions displayed during pick1 phase are: {0} with the right champion being {1}", GetKeypadChampionNames(), champion));
		return champion;
	}
	
	private Champion GenerateSecondPick()
	{
		var prevChampMaterialName = teamBlue[0].GetComponent<MeshRenderer>().material.name;
		prevChampMaterialName = prevChampMaterialName.Substring(0, prevChampMaterialName.Length - 11);
		var prevChamp = EnumUtil.ParseEnum<Champion>(prevChampMaterialName);
		
		var champsToPickFrom = new Champion[_championsList.Length];
		Array.Copy(_championsList, champsToPickFrom, _championsList.Length);
		
		string champRole;
		// ReSharper disable once ConvertSwitchStatementToSwitchExpression
		switch (GetChampionRole(prevChamp))
		{
			case "top":
				champRole = "mid";
				break;
			case "jungle":
				champRole = "bot";
				break;
			case "mid":
				champRole = "support";
				break;
			case "bot":
				champRole = "jungle";
				break;
			case "support":
				champRole = "top";
				break;
			default:
				throw new Exception("Invalid role");
		}
		
		var champClass = GetChampionClass(prevChamp);
		var champDifficulty = GetChampionDifficulty(prevChamp);
		
		champsToPickFrom.LeftShift(Array.IndexOf(champsToPickFrom, prevChamp));
		champsToPickFrom = champsToPickFrom.Skip(1).ToArray();

		var champion = Champion.aatrox; //TODO: make work without having to set default
		var championFound = false;
		
		foreach (var t in champsToPickFrom)
		{
			// ReSharper disable once InvertIf
			if (champRole == GetChampionRole(t) && champClass == GetChampionClass(t) && champDifficulty == GetChampionDifficulty(t))
			{
				champion = t;
				championFound = true;
				break;
			}
		}

		if (!championFound) // check if the champion isn't banned
		{
			champion = champsToPickFrom[0];
			 do {
				 champion = champsToPickFrom[(Array.IndexOf(champsToPickFrom, champion) + 1)% champsToPickFrom.Length];
				 LOG(string.Format("Phase 2: The previous champion was not available or did not have the right role, the correct champion is now {0}", champion));
			 } while (_bannedChampions.Contains(champion) || champRole != GetChampionRole(champion));
			 
		}
		
		var screenCharacters = GenerateUniqueRandomRange(0, _selectableChampions.Count, 6, new List<int>(){_selectableChampions.IndexOf(champion)});
		for (var i = 0; i < keypad.Length; i++)
		{
			SetMaterialChampion(keypad[i], _selectableChampions[screenCharacters[i]]);
		}
		SetMaterialChampion( keypad[Random.Range(0,keypad.Length)], champion);
		LOG(string.Format("The champions displayed during pick2 phase are: {0} with the right champion being {1}", GetKeypadChampionNames(), champion));
		
		return champion;
	}
	
	private Champion GenerateThirdPick()
	{
		var prevChampMaterialName = teamBlue[0].GetComponent<MeshRenderer>().material.name;
		prevChampMaterialName = prevChampMaterialName.Substring(0, prevChampMaterialName.Length - 11);
		var prevChamp = EnumUtil.ParseEnum<Champion>(prevChampMaterialName);
		
		var champsToPickFrom = new Champion[_championsList.Length];
		Array.Copy(_championsList, champsToPickFrom, _championsList.Length);
		
		string champRole;
		// ReSharper disable once ConvertSwitchStatementToSwitchExpression
		switch (GetChampionRole(prevChamp))
		{
			case "top":
				champRole = "jungle";
				break;
			case "jungle":
				champRole = "mid";
				break;
			case "mid":
				champRole = "top";
				break;
			case "bot":
				champRole = "support";
				break;
			case "support":
				champRole = "bot";
				break;
			default:
				throw new Exception("Invalid role");
		}
		
		var champClass = GetChampionClass(prevChamp);
		var champDifficulty = GetChampionDifficulty(prevChamp);
		
		champsToPickFrom.LeftShift(Array.IndexOf(champsToPickFrom, prevChamp));
		champsToPickFrom = champsToPickFrom.Skip(1).ToArray();
		
		var champion = Champion.aatrox; //TODO: make work without having to set default
		var championFound = false;

		foreach (var t in champsToPickFrom)
		{
			// ReSharper disable once InvertIf
			if (champRole == GetChampionRole(t) && champClass == GetChampionClass(t) && champDifficulty == GetChampionDifficulty(t))
			{
				champion = t;
				championFound = true;
				break;
			}
		}

		if (!championFound) // check if the champion isn't banned
		{
			champion = champsToPickFrom[0];
			 do
			 {
				 champion = champsToPickFrom[(Array.IndexOf(champsToPickFrom, champion) + 1)% champsToPickFrom.Length];
				 LOG(string.Format("Phase 3: The previous champion was not available or did not have the right role, the correct champion is now {0}", champion));
			 } while (_bannedChampions.Contains(champion) || champRole != GetChampionRole(champion));
		}
		
		var screenCharacters = GenerateUniqueRandomRange(0, _selectableChampions.Count, 6, new List<int>(){_selectableChampions.IndexOf(champion)});
		for (var i = 0; i < keypad.Length; i++)
		{
			SetMaterialChampion(keypad[i], _selectableChampions[screenCharacters[i]]);
		}
		SetMaterialChampion( keypad[Random.Range(0,keypad.Length)], champion);
		LOG(string.Format("The champions displayed during pick3 phase are: {0} with the right champion being {1}", GetKeypadChampionNames(), champion));
		
		return champion;
	}
	
	private Champion GenerateFourthPick()
	{
		Champion champion;
		if (GetRedChampions().Contains("vi") && GetRedChampions().Contains("caitlyn") && _selectableChampions.Contains(Champion.jinx)) champion = Champion.jinx;
		else if (GetRedChampions().Contains("lucian") && GetRedChampions().Contains("senna") && _selectableChampions.Contains(Champion.thresh)) champion = Champion.thresh;
		else if (GetBlueChampions().Contains("ezreal") && !GetBlueChampions().Contains("lux") && _selectableChampions.Contains(Champion.zoe)) champion = Champion.zoe;
		else if (GetBlueChampions().Contains("gwen") && GetBlueChampions().Contains("senna") && _selectableChampions.Contains(Champion.akshan)) champion = Champion.akshan;
		else if (GetBlueChampions().Contains("tristana") && _selectableChampions.Contains(Champion.rumble)) champion = Champion.rumble;
		else if (GetRedChampions().Contains("singed") && _selectableChampions.Contains(Champion.warwick)) champion = Champion.warwick;
		else if (GetRedChampions().Contains("jhin") && _selectableChampions.Contains(Champion.zed)) champion = Champion.zed;
		else if (GetRedChampions().Contains("aatrox") && _selectableChampions.Contains(Champion.kled)) champion = Champion.kled;
		else if (GetRedChampions().Contains("pantheon") && _selectableChampions.Contains(Champion.aurelionsol)) champion = Champion.aurelionsol;
		else if (GetRedChampions().Contains("viego") && _selectableChampions.Contains(Champion.gwen)) champion = Champion.gwen;
		else if (GetRedChampions().Contains("gwen") && _selectableChampions.Contains(Champion.viego)) champion = Champion.viego;
		else if (GetBlueChampions().Contains("yuumi") && _selectableChampions.Contains(Champion.twitch)) champion = Champion.twitch;
		else if (GetBlueChampions().Contains("akali") && _selectableChampions.Contains(Champion.shen)) champion = Champion.shen;
		else if (GetRedChampions().Contains("ryze") && _selectableChampions.Contains(Champion.brand)) champion = Champion.brand;
		else if (GetRedChampions().Contains("brand") && _selectableChampions.Contains(Champion.ryze)) champion = Champion.ryze;
		else if (GetBlueChampions().Contains("lucian") && _selectableChampions.Contains(Champion.senna)) champion = Champion.senna;
		else if (GetBlueChampions().Contains("senna") && _selectableChampions.Contains(Champion.lucian)) champion = Champion.lucian;
		else if (GetRedChampions().Contains("sylas") && _selectableChampions.Contains(Champion.garen)) champion = Champion.garen;
		else if (GetRedChampions().Contains("garen") && _selectableChampions.Contains(Champion.sylas)) champion = Champion.sylas;
		else if (GetRedChampions().Contains("diana") && _selectableChampions.Contains(Champion.leona)) champion = Champion.leona;
		else if (GetRedChampions().Contains("leona") && _selectableChampions.Contains(Champion.diana)) champion = Champion.diana;
		else if (GetRedChampions().Contains("cassiopeia") && _selectableChampions.Contains(Champion.katarina)) champion = Champion.katarina;
		else if (GetRedChampions().Contains("darius") && _selectableChampions.Contains(Champion.garen)) champion = Champion.garen;
		else if (GetRedChampions().Contains("garen") && _selectableChampions.Contains(Champion.darius)) champion = Champion.darius;
		else if (GetRedChampions().Contains("teemo") && _selectableChampions.Contains(Champion.ryze)) champion = Champion.ryze;
		//TODO: check capitalisation
		else if (( GetBlueChampions().Contains("Varus") || GetBlueChampions().Contains("Aatrox") ) && _selectableChampions.Contains(Champion.thresh)) champion = Champion.thresh;
		else if (GetBlueChampions().Contains("xayah") && _selectableChampions.Contains(Champion.rakan)) champion = Champion.rakan;
		else if (GetBlueChampions().Contains("rakan") && _selectableChampions.Contains(Champion.xayah)) champion = Champion.xayah;
		else if (GetBlueChampions().Contains("kogmaw") && _selectableChampions.Contains(Champion.lulu)) champion = Champion.lulu;
		else if (GetBlueChampions().Contains("lulu") && _selectableChampions.Contains(Champion.kogmaw)) champion = Champion.kogmaw;
		else if (GetRedChampions().Contains("rengar") && _selectableChampions.Contains(Champion.khazix)) champion = Champion.khazix;
		else if (GetRedChampions().Contains("khazix") && _selectableChampions.Contains(Champion.rengar)) champion = Champion.rengar;
		else if (GetBlueChampions().Contains("ashe") && _selectableChampions.Contains(Champion.tryndamere)) champion = Champion.tryndamere;
		else if (GetRedChampions().Contains("seraphine") && _selectableChampions.Contains(Champion.skarner)) champion = Champion.skarner;
		else if (GetRedChampions().Contains("volibear") && _selectableChampions.Contains(Champion.ornn)) champion = Champion.ornn;
		else if (GetBlueChampions().Contains("heimerdinger") && _selectableChampions.Contains(Champion.ziggs)) champion = Champion.ziggs;
		else if (GetBlueChampions().Contains("ziggs") && _selectableChampions.Contains(Champion.heimerdinger)) champion = Champion.heimerdinger;
		else if (GetBlueChampions().Contains("blitzcrank") && _selectableChampions.Contains(Champion.viktor)) champion = Champion.viktor;
		else if (GetRedChampions().Contains("akali") && _selectableChampions.Contains(Champion.malzahar)) champion = Champion.malzahar;
		else if (GetBlueChampions().Contains("ivern") && _selectableChampions.Contains(Champion.maokai)) champion = Champion.maokai;
		else if (GetBlueChampions().Contains("maokai") && _selectableChampions.Contains(Champion.ivern)) champion = Champion.ivern;
		else if (GetBlueChampions().Contains("trundle") && _selectableChampions.Contains(Champion.lissandra)) champion = Champion.lissandra;
		else if (GetBlueChampions().Contains("yone") && _selectableChampions.Contains(Champion.yasuo)) champion = Champion.yasuo;
		else if (GetBlueChampions().Contains("yasuo") && _selectableChampions.Contains(Champion.yone)) champion = Champion.yone;
		else if (GetBlueChampions().Contains("neeko") && _selectableChampions.Contains(Champion.nidalee)) champion = Champion.nidalee;
		else if (GetBlueChampions().Contains("nidalee") && _selectableChampions.Contains(Champion.neeko)) champion = Champion.neeko;
		else if (GetRedChampions().Contains("evelynn") && _selectableChampions.Contains(Champion.vayne)) champion = Champion.vayne;
		else if (GetBlueChampions().Contains("twistedfate") && _selectableChampions.Contains(Champion.graves)) champion = Champion.graves;
		else if (GetBlueChampions().Contains("garen") && _selectableChampions.Contains(Champion.lux)) champion = Champion.lux;
		else if (GetBlueChampions().Contains("lux") && _selectableChampions.Contains(Champion.garen)) champion = Champion.garen;
		else if (GetBlueChampions().Contains("wukong") && _selectableChampions.Contains(Champion.masteryi)) champion = Champion.masteryi;
		else if (GetBlueChampions().Contains("masteryi") && _selectableChampions.Contains(Champion.wukong)) champion = Champion.wukong;
		else if (GetRedChampions().Contains("sion") && _selectableChampions.Contains(Champion.jarvaniv)) champion = Champion.jarvaniv;
		else if (GetRedChampions().Contains("jarvaniv") && _selectableChampions.Contains(Champion.sion)) champion = Champion.sion;
		else if (GetRedChampions().Contains("urgot") && _selectableChampions.Contains(Champion.caitlyn)) champion = Champion.caitlyn;
		else if (GetBlueChampions().Contains("malzahar") && _selectableChampions.Contains(Champion.kogmaw)) champion = Champion.kogmaw;
		else if (GetBlueChampions().Contains("aphelios") && _selectableChampions.Contains(Champion.thresh)) champion = Champion.thresh;
		else if (GetRedChampions().Contains("kayle") && _selectableChampions.Contains(Champion.morgana)) champion = Champion.morgana;
		else if (GetRedChampions().Contains("morgana") && _selectableChampions.Contains(Champion.kayle)) champion = Champion.kayle;
		else if (GetRedChampions().Contains("xerath") && _selectableChampions.Contains(Champion.azir)) champion = Champion.azir;
		else if (GetRedChampions().Contains("azir") && _selectableChampions.Contains(Champion.xerath)) champion = Champion.xerath;
		else if (GetBlueChampions().Contains("draven") && _selectableChampions.Contains(Champion.blitzcrank)) champion = Champion.blitzcrank;
		else if (GetBlueChampions().Contains("blitzcrank") && _selectableChampions.Contains(Champion.draven)) champion = Champion.draven;
		else if(_selectableChampions.Contains(Champion.teemo)) champion = Champion.teemo;
		else throw new Exception(
			"I didn't expect you would ever receive this error, please sent me a message on discord/steam with all the info");
		
		var screenCharacters = GenerateUniqueRandomRange(0, _selectableChampions.Count, 6, new List<int>(){_selectableChampions.IndexOf(champion)});
		for (var i = 0; i < keypad.Length; i++)
		{
			SetMaterialChampion(keypad[i], _selectableChampions[screenCharacters[i]]);
		}
		SetMaterialChampion( keypad[Random.Range(0,keypad.Length)], champion);
		LOG(string.Format("The champions displayed during pick4 phase are: {0} with the right champion being {1}", GetKeypadChampionNames(), champion));

		return champion;
	}
	
	/**
	*	sets a square to a champion material
	*/
	private void SetMaterialChampion(Component thing, Champion championName)
	{
		thing.GetComponent<MeshRenderer>().material = championMaterials[Array.IndexOf(_championsList,championName)];
	} 

	/**
	 *	gets the name of all champions on the keypad 
	 */
	private string GetKeypadChampionNames()
	{
		var ans = keypad.Select(key => key.GetComponent<MeshRenderer>().material.name).Select(champion => champion.Substring(0, champion.Length - 11)).ToList();
		return ans.Join(", ");
	}

	
	/**
	*	generates a random champion from a given list
	*/
	// ReSharper disable Unity.PerformanceAnalysis
	private IEnumerator GenerateChampion(int championNumber, List<Champion> championsToChoseFrom = null, int delay = -1)
	{
		if (delay < 0) delay = PickDelay;
		if (championsToChoseFrom == null) championsToChoseFrom = _selectableChampions;

		_animationPlaying++;

		var champion = championsToChoseFrom[Random.Range(0, championsToChoseFrom.Count)];
		while (!_selectableChampions.Contains(champion))
		{
			champion = championsToChoseFrom[Random.Range(0, championsToChoseFrom.Count)];
		}
		_selectableChampions.RemoveAll(x => x == champion);

		yield return new WaitForSeconds(delay);
		SetMaterialChampion(teamRed[championNumber], champion);
		
		_animationPlaying--;
	}
	
	/**
	*	generates a random number from a given size
	*/
	private static List<int> GenerateUniqueRandomRange(int min, int max, int amount = 1, List<int> usedValues = null)
	{
		if (max - min < amount) throw new Exception("Invalid amount compared to size to chose from");
		if(usedValues==null) usedValues= new List<int>();
		
		var result = new List<int>();
		for (var i = 0; i < amount; i++)
		{
			var val = Random.Range(min, max);
			while (usedValues.Contains(val))
			{
				val = Random.Range(min, max);
			}
			usedValues.Add(val);
			result.Add(val);
		}

		return result;
	} 

	private List<string> GetBlueChampions()
	{
		var champions = teamBlue.Select(rendererObject => rendererObject.GetComponent<MeshRenderer>().material.name).Select(champion => champion.Substring(0, champion.Length - 11)).ToList();
		return champions;
	}
	
	private List<string> GetRedChampions()
	{
		var champions = teamRed.Select(rendererObject => rendererObject.GetComponent<MeshRenderer>().material.name).Select(champion => champion.Substring(0, champion.Length - 11)).ToList();
		return champions;
	}
	
	private static string GetChampionRole(Champion champion)
	{
		if (Constants.Top().Contains(champion)) return "top";
		if (Constants.Jungle().Contains(champion)) return "jungle";
		if (Constants.Mid().Contains(champion)) return "mid";
		return Constants.Bot().Contains(champion) ? "bot" : "support";
	}
	
	private static string GetChampionClass(Champion champion)
	{
		if (Constants.Assassin().Contains(champion)) return "assassin";
		if (Constants.Fighter().Contains(champion)) return "fighter";
		if (Constants.Mage().Contains(champion)) return "mage";
		if (Constants.Marksman().Contains(champion)) return "marksman";
		return Constants.Support().Contains(champion) ? "support" : "tank";
	}
	
	private static int GetChampionDifficulty(Champion champion)
	{
		if (Constants.Difficulty1().Contains(champion)) return 1;
		if (Constants.Difficulty2().Contains(champion)) return 2;
		if (Constants.Difficulty3().Contains(champion)) return 3;
		else throw new Exception(string.Concat("could not find difficulty of the champion: {0}", champion));
	}
	
	/**
	 *	handles strikes
	 */
	private void HandleStrike(Champion correctAnswer, Champion answer)
	{
		LOG(string.Format("Strike! Expected {0} but {1} was pressed", correctAnswer, answer));
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
		GetComponent<KMBombModule>().HandleStrike();
	}
	
	/**
	 *	handles the module being solved
	 */
	private void HandleSolved()
	{
		LOG("Module solved");
		audio.HandlePlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		GetComponent<KMBombModule>().HandlePass();
	}

	private void LOG(string message)
	{
		Debug.LogFormat("[Drafting #{0}] " + message, _moduleId);
	}
}