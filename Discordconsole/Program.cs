﻿using Discord;
using Discord.Audio;
using Discord.Commands;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Configuration;
using System.Threading.Tasks;
using System.IO;
using System.Web.Helpers;
using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Collections.Generic;

namespace Discordconsole
{
    public static class NonBlockingConsole
    {
        private static BlockingCollection<object> m_Queue = new BlockingCollection<object>();

        static NonBlockingConsole()
        {
            var thread = new Thread(
              () =>
              {
                  while (true) Console.WriteLine(m_Queue.Take());
              });
            thread.IsBackground = true;
            thread.Start();
        }

        public static void WriteLine(object value)
        {
            m_Queue.Add($"[{DateTime.Now}] : " + value);
        }
    }

    class Program

    {
        #region PreInit
        public readonly string version = "v0.4";
        private bool shutup = false;
        static void Main() => new Program().Start();

        private DiscordClient _client;
        public void SendConsole(object text)
        {
            NonBlockingConsole.WriteLine(text);
        }
        #endregion

        #region Input

        private static async Task<string> GetInputAsync()
        {
            return await Task.Run(() => Console.ReadLine());
        }
        #endregion
        #region SendAudio

        public bool? SendAudio(Channel voiceChannel, IAudioClient _vClient, int quality = 20)
        {
            bool isFinished = false;
            var channelCount = _client.GetService<AudioService>().Config.Channels; // Get the number of AudioChannels our AudioService has been configured to use.
            var OutFormat = new WaveFormat(48000, 16, channelCount); // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
            MemoryStream mp3file = new MemoryStream(File.ReadAllBytes(AppDomain.CurrentDomain.BaseDirectory + "topkek.mp3")/* Properties.Resources.topkek */);

            using (var MP3Reader = new Mp3FileReader(mp3file)) // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
            using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat)) // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
            {
                resampler.ResamplerQuality = quality; // Set the quality of the resampler to 20, a good quality
                int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0) // Read audio into our buffer, and keep a loop open while data is present
                {
                    if (byteCount < blockSize)
                    {
                        // Incomplete Frame
                        for (int i = byteCount; i < blockSize; i++)
                            buffer[i] = 0;
                    }

                    _vClient.Send(buffer, 0, blockSize);
                    NonBlockingConsole.WriteLine($"omg i sent something ? noh ?");// Send the buffer to Discord

                }
                isFinished = true;
            }
            return isFinished;
        }
        public bool? SendAudio(Channel voiceChannel, IAudioClient _vClient, CancellationTokenSource cancel, int quality = 20)
        {
            bool isFinished = false;
            var channelCount = _client.GetService<AudioService>().Config.Channels; // Get the number of AudioChannels our AudioService has been configured to use.
            MemoryStream mp3file;
            var OutFormat = new WaveFormat(48000, 16, channelCount); // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
            try
            {
                mp3file = new MemoryStream(File.ReadAllBytes(AppDomain.CurrentDomain.BaseDirectory + "topkek.mp3"));
            }
            catch (Exception ex)
            {
                NonBlockingConsole.WriteLine($"Ok so {AppDomain.CurrentDomain.BaseDirectory} and also {ex.Message} with {ex.StackTrace}");
                Console.ReadKey();
                throw;
            }

            using (var MP3Reader = new Mp3FileReader(mp3file)) // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
            using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat)) // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
            {
                resampler.ResamplerQuality = quality; // Set the quality of the resampler to 20, a good quality
                int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0 && !cancel.IsCancellationRequested) // Read audio into our buffer, and keep a loop open while data is present
                {
                    try
                    {
                        if (byteCount < blockSize)
                        {
                            // Incomplete Frame
                            for (int i = byteCount; i < blockSize; i++)
                                buffer[i] = 0;
                        }
                        if (!cancel.IsCancellationRequested)
                            _vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord

                    }


                    catch (OperationCanceledException)
                    {
                        _vClient.Channel.LeaveAudio();
                    }
                }
            }
            isFinished = true;
            return isFinished;
        }

        private CancellationTokenSource token = new CancellationTokenSource();

        public async Task<bool?> SendAudioAsync(Channel voiceChannel, IAudioClient _vClient, int quality = 20)
        {

            return await Task.Run(() => SendAudio(voiceChannel, _vClient, quality));
        }
        public async Task<bool?> SendAudioAsync(Channel voiceChannel, IAudioClient _vClient, CancellationTokenSource cancel, int quality = 20)
        {

            return await Task.Run(() => SendAudio(voiceChannel, _vClient, cancel, quality));
        }
        #endregion
        ~Program()
        {
            NonBlockingConsole.WriteLine("Bye");  
        }
        public void Start()
        {
            
            #region InitClient

            _client = new DiscordClient();
            #endregion

            #region Commands
            _client.UsingCommands(x =>
            {
                x.PrefixChar = '$';
                x.HelpMode = HelpMode.Public;

            });

            // -------

            _client.GetService<CommandService>().CreateCommand("roll") //create command greet
        .Description("Roll a dice between <from> to <to>") //add description, it will be shown when ~help is used
        .Parameter("From", ParameterType.Required) //as an argument, we have a person we want to greet
        .Parameter("To", ParameterType.Required)

        .Do(async e =>
        {
            int randResult = 1;

            bool woot = true;
            try
            {
                randResult = new Random().Next(int.Parse(e.GetArg("From")), (int.Parse(e.GetArg("To"))));
            }
            catch (Exception)
            {
                await e.Channel.SendMessage("The number was out of bounds, sorry :frowning:");
                woot = false;
            }
            if (woot)
            {
                await e.Channel.SendMessage($"This roll made {randResult} ! ");
                if (randResult == 69)
                    await e.Channel.SendMessage(@"Kek it is 69 ¯\_(ツ)_/¯");



            }




            //sends a message to channel with the given text
        });




            _client.GetService<CommandService>().CreateCommand("shutup") //create command greet (totally kek)
        .Description("Says violently to the bot \"SHUT UP PLEASE\"") //add description, it will be shown when ~help is used
        .Alias(new string[] { "stfu", "shutthefuckup" })
        .Parameter("kekle", ParameterType.Unparsed)
        .Do(async e =>
        {
            if (shutup)
            {
                await e.Channel.SendMessage("Yay :) I can now share the kek with everyone ! :cake: ");
                _client.SetGame(new Game($"Keking everyone ! {version}"));
                shutup = false;
            }
            else
            {
                await e.Channel.SendMessage("Ok... :cry: ");
                _client.SetGame(new Game($"Doing nothing, {version}"));
                shutup = true;
            }


            //sends a message to channel with the given text
        });
            Channel voiceChannel = null;
            IAudioClient _vClient = null;
            bool? isComplete = true;

            _client.GetService<CommandService>().CreateCommand("stopmusic")
            .Description("Stops the music")
            .Do(async e =>
                {

                    try
                    {
                        token.Cancel();
                        await voiceChannel.LeaveAudio();


                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is NullReferenceException)
                    {
                        await e.Channel.SendMessage("There is no audio playing for now");
                    }
                    finally
                    {
                        await e.Channel.SendMessage("Audio stopped.");
                    }

                });
            #endregion

            _client.MessageReceived += async (s, e) =>
            {
                //if (!e.Message.IsAuthor)
                //    await e.Channel.SendMessage(e.Message.Text);



                if (Regex.IsMatch(e.Message.Text.ToLower(), "ke{1,100}k") && !e.Message.IsAuthor && !e.Message.IsMentioningMe() && !shutup && !e.Message.User.IsBot)
                {
                    await e.Channel.SendMessage($"Oh yeah ! {e.User.NicknameMention} is the kekkest guy ever !");
                    SendConsole($"Someone : {e.User.Name} has said topkek, message sent :)");

                    try
                    {
                        voiceChannel = _client.FindServers(e.Server.Name).FirstOrDefault().VoiceChannels.ToList().Find((Channel c) =>
                        {

                            return c.Name.ToLower().Contains("kek") == true || c.Name.ToLower().Contains("General") == true;
                        });
                        _vClient = await _client.GetService<AudioService>() // We use GetService to find the AudioService that we installed earlier. In previous versions, this was equivelent to _client.Audio()
                        .Join(voiceChannel);

                    }
                    catch (NullReferenceException)
                    {
                        await e.Channel.SendMessage("No voice channels that contains \"General\" or \"kek\" has been found. Canceling music :(");
                    }
                    catch (Exception ex)
                    {
                        await e.Channel.SendMessage($"wtf i dunno wat happened so i sent you that hope you like this ```{ex.Message} OMG AND ALSO {ex.StackTrace}```");
                    }
                    if (new Random().Next(0, 3) == 1 && voiceChannel != null)
                    {
                        await _vClient.Join(voiceChannel);
                        await e.Channel.SendMessage("LET THE KEK PLAYS");
                        NonBlockingConsole.WriteLine("Is complete : " + isComplete);
                        if (isComplete == true || isComplete == null)
                        {
                            isComplete = false;
                            token = new CancellationTokenSource();
                            isComplete = await SendAudioAsync(voiceChannel, _vClient, token, 20);
                        }

                    }

                }


            };
            _client.UsingAudio(x =>
            {
                x.Mode = AudioMode.Both;


            });

            #region ConnectingAndTokenPrompt
            _client.ExecuteAndWait(async () =>
               {
                   
                   string localToken = null;
                   StreamReader file = null;

                   try
                   {
                       file = File.OpenText(AppDomain.CurrentDomain.BaseDirectory + "token.tok");
                   }
                   catch (Exception)
                   {
                       Console.WriteLine("! -> File not found or inacessible.");
                       goto Start;
                   }

                       string json = file?.ReadToEnd();
                       localToken = json;
                       file?.Close();
                #region kekkle
                /* catch (Exception)
                           //{

                           //    if (!(file == null)) // If the file is here
                           //        file.Close(); // close it xd

                           //    Console.WriteLine("Please, insert the token here");
                           //    Console.Out.Flush();
                           //    string token = await GetInputAsync();
                           //    using (StreamWriter Tempfile = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "token.json"))
                           //    {

                           //        Tempfile.WriteLine(Json.Encode(token));
                           //        Tempfile.Close();
                           //    }
                           //    localToken = token;
                           //} */ 
                #endregion
                Start:
                   try
                   {
                       Console.WriteLine($"Alright : {Environment.NewLine} da token is {localToken ?? "null :("}");
                       await _client.Connect(localToken, TokenType.Bot);
                   }
                   catch (Exception)
                   {
                       await Task.Delay(3000);
                           file?.Close(); // close it xd
                       
                       Console.WriteLine("Please, insert the token here");
                       Console.Out.Flush();
                       string token = await GetInputAsync();
                       using (StreamWriter Tempfile = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "token.json"))
                       {
                           Tempfile.WriteLine(Json.Encode(token));
                           Tempfile.Close();
                       }
                       localToken = token;
                       goto Start;
                   }

                   SendConsole("Connected!");
                   _client.SetGame(new Game($"Keking everyone ! {version}"));
               });
            #endregion

        }
    }
}
