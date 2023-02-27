﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EleCho.GoCqHttpSdk;
using EleCho.GoCqHttpSdk.Action;
using EleCho.GoCqHttpSdk.Message;
using EleCho.GoCqHttpSdk.MessageMatching;
using EleCho.GoCqHttpSdk.Post;
//using EleCho.GoCqHttpSdk.MessageMatching;

#nullable enable

namespace AssemblyCheck
{
    internal class Program
    {
        public const int WebSocketPort = 5701;

        static CqRHttpSession rHttpSession = new CqRHttpSession(new CqRHttpSessionOptions()
        {
            BaseUri = new Uri($"http://localhost:5701"),
        });

        static CqWsSession session = new CqWsSession(new CqWsSessionOptions()
        {
            BaseUri = new Uri($"ws://127.0.0.1:{WebSocketPort}"),
        });

        private static async Task Main(string[] args)
        {
            session.UseGroupMessage(async context =>
            {
                if (context.Message.Text.StartsWith("echo "))
                {
                    await session.SendGroupMessageAsync(context.GroupId, new CqMessage(context.Message.Text.Substring(5)));
                }

            });

            Console.WriteLine("OK");
            await session.RunAsync();
        }

        private static void CheckAssemblyTypes(Assembly asm)
        {

        }
    }

    class MessageMatchPlugin1 : CqMessageMatchPostPlugin
    {
        public MessageMatchPlugin1(ICqActionSession actionSession)
        {
            ActionSession = actionSession;
        }

        public ICqActionSession ActionSession { get; }


        [CqMessageMatch("^echo (?<content>.*)")]
        public async Task Echo(CqGroupMessagePostContext context, string content)
        {
            await ActionSession.SendGroupMessageAsync(context.GroupId, new CqMessage(content));
        }

        [CqMessageMatch("^slide (?<content>.*)")]
        public async Task Slide(CqGroupMessagePostContext context, string content)
        {
            var slices =
                await ActionSession.GetWordSlicesAsync(content);
            if (slices == null)
                return;

            await ActionSession.SendGroupMessageAsync(context.GroupId, new CqMessage(string.Join(", ", slices.Slices)));
        }

        [CqMessageMatch("^testFile (?<name>.*)")]
        public async Task TestFile(CqMessagePostContext context, string name)
        {
            var testContent =
                $"""
                现在时间: {DateTime.Now};
                随机 GUID: {Guid.NewGuid()}
                """;

            FileInfo fileInfo = new FileInfo(name);

            using (var file = fileInfo.OpenWrite())
            {
                using var writer = new StreamWriter(file);

                await writer.WriteAsync(testContent);
            }

            if (context is CqGroupMessagePostContext groupContext)
            {
                await ActionSession.UploadGroupFileAsync(groupContext.GroupId, fileInfo.FullName, name);
            }
            else if (context is CqPrivateMessagePostContext privateContext)
            {
                await ActionSession.UploadPrivateFileAsync(privateContext.UserId, fileInfo.FullName, name);
            }
        }
    }

    class OpenAiMatchPlugin : CqMessageMatchPostPlugin
    {
        public OpenAiMatchPlugin(ICqActionSession actionSession, string apikey)
        {
            Client = new HttpClient();
            ActionSession = actionSession;
            Apikey = apikey;
        }


        public HttpClient Client { get; }
        public ICqActionSession ActionSession { get; }
        public string Apikey { get; }


        public class davinci_result
        {
            public class davinci_result_choices
            {
                public string text { get; set; }
                public int index { get; set; }
                public object logprobs { get; set; }
                public string finish_reason { get; set; }
            }
            public class davinci_result_usage
            {
                public int prompt_tokens { get; set; }
                public int completion_tokens { get; set; }
                public int total_tokens { get; set; }
            }
            public string id { get; set; }
            public string @object { get; set; }
            public int created { get; set; }
            public string model { get; set; }
            public davinci_result_choices[] choices { get; set; }
            public davinci_result_usage usage { get; set; }
        }


        [CqMessageMatch("^davinci (?<prompt>.+)")]
        public async void Davinci(CqMessagePostContext context, string prompt)
        {
            var request =
                new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/completions")
                {
                    Headers =
                    {
                        { "Authorization", $"Bearer {Apikey}" }
                    },

                    Content = JsonContent.Create(
                        new
                        {
                            model = "text-davinci-003",
                            prompt = $"回答这句话: {prompt}\n",
                            max_tokens = 2048,
                            temperature = 0.5,
                        }),
                };

            var response = await Client.SendAsync(request);
            var davinci_rst = await response.Content.ReadFromJsonAsync<davinci_result>();

            if (davinci_rst == null)
                return;

            var davinci_rst_txt =
                string.Join(Environment.NewLine, davinci_rst.choices.Select(choice => choice.text)).Trim();

            if (context is CqGroupMessagePostContext groupContext)
            {
                await ActionSession.SendGroupMessageAsync(groupContext.GroupId, new CqMessage(davinci_rst_txt));
            }
            else if (context is CqPrivateMessagePostContext privateContext)
            {
                await ActionSession.SendPrivateMessageAsync(privateContext.UserId, new CqMessage(davinci_rst_txt));
            }
        }



        public async Task ImageEdit(CqMessagePostContext context)
        {

        }
    }

    class MessageMatchPlugin2 : CqMessageMatchPostPlugin
    {
        public MessageMatchPlugin2(ICqActionSession actionSession)
        {
            ActionSession = actionSession;
        }

        public ICqActionSession ActionSession { get; }


        [CqMessageMatch("^repeat (?<content>.*)")]
        public async Task Echo(CqGroupMessagePostContext context, string content)
        {
            await ActionSession.SendGroupMessageAsync(context.GroupId, new CqMessage(content));
        }
    }
}