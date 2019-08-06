// © 2019 Linden Research, Inc.

using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Collections.Generic;

public class MediaChatCommand : SceneObjectScript
{
    [Tooltip("All users can type media chat commands if this is on")]
    [DisplayName("Restricted Access")]
    [DefaultValue(true)]
    public bool RestrictedAccess;

    [Tooltip("Comma separated list of user handles for those granted restricted access")]
    [DisplayName("Restricted Access User Handles")]
    public string AllowedAgents;

    private HashSet<string> _allowedAgents;
    private Dictionary<string, string> _commandsUsage;

    public override void Init()
    {
        _allowedAgents = SplitStringIntoHashSet(AllowedAgents);

        _commandsUsage = new Dictionary<string, string>
        {
            { "/help", "" },
            { "/media", "[url]" },
            { "/yt", "[video-id] (optional start time)" },
            { "/youtube", "[video-id] (optional start time)" },
            { "/ytlive", "[channel-name]" },
            { "/ytpl", "[playlist-id]" },
            { "/twitch", "[channel-name]" },
            { "/twitchv", "[video-id]" },
            { "/vimeo", "[video-id]" },
        };

        ScenePrivate.Chat.Subscribe(Chat.DefaultChannel, OnChat);          /////// start listening to chat NearBy, an Event maybe
    }

    HashSet<string> SplitStringIntoHashSet(string commaSeparatedString)  ///////////////// A Methode to convert commaSeprated names into the Hash
    {
        HashSet<string> hash = new HashSet<string>();

        string[] separateStrings = commaSeparatedString.Split(',');
        for (int i = 0; i < separateStrings.Length; ++i)
        {
            string separateString = separateStrings[i].Trim();
            if (!string.IsNullOrWhiteSpace(separateString))
            {
                hash.Add(separateString);
            }
        }

        return hash;
    }

    bool IsAccessAllowed(AgentPrivate agent)                          /////////////// a methode to check if the input agent is allowed
    {
        if (RestrictedAccess)
        {
            bool accessAllowed = false;

            if (agent != null)
            {
                // Always allow the creator of the scene
                accessAllowed |= (agent.AgentInfo.AvatarUuid == ScenePrivate.SceneInfo.AvatarUuid);      // check if u r admin

                // Authenticate other allowed agents
                accessAllowed |= _allowedAgents.Contains(agent.AgentInfo.Handle);               // get an agent name
            }

            return accessAllowed;
        }

        return true;
    }

    void OnChat(ChatData data)
    {
        AgentPrivate agent = ScenePrivate.FindAgent(data.SourceId);            // get the chat sender avatar name
        if (!IsAccessAllowed(agent))                     // if you are not allowed then stop the code
            return;

        string[] chatWords = data.Message.Split(' ');            // reconstructing the chat message

        if (chatWords.Length < 1)           // stop the command if there is not command
            return;

        string command = chatWords[0];            // extract commands

        if (!_commandsUsage.ContainsKey(command))          //if the command is not in our list stop the code
            return;

        if (command == "/help" || chatWords.Length < 2)           // if command is Help then show a help list
        {
            string helpMessage = "MediaChatCommand usage:";
            foreach (var kvp in _commandsUsage)
            {
                helpMessage += "\n" + kvp.Key + " " + kvp.Value;
            }

            try                                                     // using TRY, CATCH!
            {                                                       //
                agent.SendChat(helpMessage);                        // send message to an agent (not name we need the agent type)
            }
            catch
            {
                // Agent left
            }

            return;
        }

        string medialUrl = "";

        if (command == "/media" || chatWords[1].StartsWith("http"))
        {
            medialUrl = chatWords[1];
        }
        else if (command == "/yt" || command == "/youtube")
        {
            string videoId = chatWords[1];
            medialUrl = string.Format("https://www.youtube.com/embed/{0}?autoplay=1&loop=1&playlist={0}&controls=0", videoId);

            if (chatWords.Length > 2)
            {
                int startTime = 0;
                if (int.TryParse(chatWords[2], out startTime))
                {
                    medialUrl += "&start=" + startTime;
                }
            }
        }
        else if (command == "/ytlive")
        {
            string livestreamId = chatWords[1];
            medialUrl = string.Format("https://www.youtube.com/embed/live_stream?channel={0}&autoplay=1", livestreamId);
        }
        else if (command == "/ytpl")
        {
            string playlistId = chatWords[1];
            medialUrl = string.Format("https://www.youtube.com/embed?%20listType=playlist%20&list={0}&loop=1&autoplay=1&controls=0", playlistId);
        }
        else if (command == "/twitch")
        {
            string channelName = chatWords[1];
            medialUrl = string.Format("http://player.twitch.tv/?channel={0}&quality=source&volume=1", channelName);
        }
        else if (command == "/twitchv")
        {
            string videoId = chatWords[1];
            medialUrl = string.Format("http://player.twitch.tv/?video={0}&quality=source&volume=1", videoId);
        }
        else if (command == "/vimeo")
        {
            string videoId = chatWords[1];
            medialUrl = string.Format("https://player.vimeo.com/video/{0}?autoplay=1&loop=1&autopause=0", videoId);
        }

        bool throttled = false;

        try
        {
            ScenePrivate.OverrideMediaSource(medialUrl);

            agent.SendChat("Media URL successfully updated to " + medialUrl);
            Log.Write("New media URL: " + medialUrl);
        }
        catch (ThrottleException)
        {
            throttled = true;
            Log.Write("Throttled: Unable to update media URL.");
        }
        catch
        {
            // Agent left
        }

        if (throttled)
        {
            try
            {
                agent.SendChat("Media URL update was throttled.  Try again.");
            }
            catch
            {
                // Agent left
            }
        }
    }
}
