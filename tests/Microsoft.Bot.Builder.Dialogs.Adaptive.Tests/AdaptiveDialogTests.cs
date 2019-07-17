﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Recognizers;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Events;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Actions;
using Microsoft.Bot.Builder.Dialogs.Declarative.Resources;
using Microsoft.Bot.Builder.Dialogs.Declarative.Types;
using Microsoft.Bot.Builder.Expressions;
using Microsoft.Bot.Builder.Expressions.Parser;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Bot.Builder.Dialogs.Adaptive.Tests
{
    [TestClass]
    public class AdaptiveDialogTests
    {
        public TestContext TestContext { get; set; }

        public ExpressionEngine expressionEngine { get; set; } = new ExpressionEngine();

        private TestFlow CreateFlow(AdaptiveDialog ruleDialog)
        {
            TypeFactory.Configuration = new ConfigurationBuilder().Build();

            var explorer = new ResourceExplorer();
            var storage = new MemoryStorage();
            var convoState = new ConversationState(storage);
            var userState = new UserState(storage);

            var adapter = new TestAdapter(TestAdapter.CreateConversation(TestContext.TestName));
            adapter
                .UseStorage(storage)
                .UseState(userState, convoState)
                .Use(new RegisterClassMiddleware<ResourceExplorer>(explorer))
                .UseLanguageGeneration(explorer)
                .Use(new TranscriptLoggerMiddleware(new FileTranscriptLogger()));

            DialogManager dm = new DialogManager(ruleDialog);
            return new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await dm.OnTurnAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
            });
        }

        [TestMethod]
        public async Task AdaptiveDialog_TopLevelFallback()
        {
            var ruleDialog = new AdaptiveDialog("planningTest");

            ruleDialog.AddEvent(new OnUnknownIntent(
                    new List<IDialog>()
                    {
                        new SendActivity("Hello Planning!")
                    }));

            await CreateFlow(ruleDialog)
            .Send("start")
                .AssertReply("Hello Planning!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_TopLevelFallbackMultipleActivities()
        {
            var ruleDialog = new AdaptiveDialog("planningTest");

            ruleDialog.AddEvent(new OnUnknownIntent(new List<IDialog>()
                    {
                        new SendActivity("Hello Planning!"),
                        new SendActivity("Howdy awain")
                    }));

            await CreateFlow(ruleDialog)
            .Send("start")
                .AssertReply("Hello Planning!")
                .AssertReply("Howdy awain")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_EndTurn()
        {
            var ruleDialog = new AdaptiveDialog("planningTest");

            ruleDialog.AddEvent(
                new OnUnknownIntent(
                    new List<IDialog>()
                    {
                        new TextInput()
                        {
                            Prompt = new ActivityTemplate("Hello, what is your name?"),
                            Property = "user.name"
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!"),
                    }));

            await CreateFlow(ruleDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_EditArray()
        {
            var dialog = new AdaptiveDialog("planningTest");
            dialog.Events.Add(new OnBeginDialog()
            {
                Actions = new List<IDialog>()
                {
                    // Add item
                    new TextInput() {
                        AlwaysPrompt = true,
                        Prompt = new ActivityTemplate("Please add an item to todos."),
                        Property = "dialog.todo"
                    },
                    new InitProperty() { Property = "user.todos", Type = "array" },
                    new EditArray(EditArray.ArrayChangeType.Push, "user.todos", "dialog.todo"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },
                    new TextInput()
                    {
                        AlwaysPrompt = true,
                        Prompt = new ActivityTemplate("Please add an item to todos."),
                        Property = "dialog.todo"

                    },
                    new EditArray(EditArray.ArrayChangeType.Push, "user.todos", "dialog.todo"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },

                    // Remove item
                    new TextInput() {
                        AlwaysPrompt = true,
                        Prompt = new ActivityTemplate("Enter a item to remove."),
                        Property = "dialog.todo"
                    },
                    new EditArray(EditArray.ArrayChangeType.Remove, "user.todos", "dialog.todo"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },

                    // Add item and pop item
                    new TextInput() {
                        AlwaysPrompt = true,
                        Prompt = new ActivityTemplate("Please add an item to todos."),
                        Property = "dialog.todo"
                    },
                    new EditArray(EditArray.ArrayChangeType.Push, "user.todos", "dialog.todo"),
                    new TextInput()
                    {
                        AlwaysPrompt = true,
                        Prompt = new ActivityTemplate("Please add an item to todos."),
                        Property = "dialog.todo"
                    },
                    new EditArray(EditArray.ArrayChangeType.Push, "user.todos", "dialog.todo"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },

                    new EditArray(EditArray.ArrayChangeType.Pop, "user.todos"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },

                    // Take item
                    new EditArray(EditArray.ArrayChangeType.Take, "user.todos"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },

                    // Clear list
                    new EditArray(EditArray.ArrayChangeType.Clear, "user.todos"),
                    new SendActivity() { Activity = new ActivityTemplate("Your todos: {join(user.todos, ',')}") },
                }
            });

            await CreateFlow(dialog)
            .Send("hi")
                .AssertReply("Please add an item to todos.")
            .Send("todo1")
                .AssertReply("Your todos: todo1")
                .AssertReply("Please add an item to todos.")
            .Send("todo2")
                .AssertReply("Your todos: todo1, todo2")
                .AssertReply("Enter a item to remove.")
            .Send("todo2")
                .AssertReply("Your todos: todo1")
                .AssertReply("Please add an item to todos.")
            .Send("todo3")
                .AssertReply("Please add an item to todos.")
            .Send("todo4")
                .AssertReply("Your todos: todo1, todo3, todo4")
                .AssertReply("Your todos: todo1, todo3")
                .AssertReply("Your todos: todo3")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_IfProperty()
        {
            var ruleDialog = new AdaptiveDialog("planningTest");

            ruleDialog.AddEvent(new OnUnknownIntent(
                    new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Actions = new List<IDialog>()
                            {
                                new TextInput() {
                                    Prompt = new ActivityTemplate("Hello, what is your name?"),
                                    Property = "user.name"
                                },
                            }
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    }));

            await CreateFlow(ruleDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_TextInput()
        {
            var ruleDialog = new AdaptiveDialog("planningTest")
            {
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new IfCondition()
                            {
                                Condition = "user.name == null",
                                Actions = new List<IDialog>()
                                {
                                    new TextInput()
                                    {
                                        Prompt = new ActivityTemplate("Hello, what is your name?"),
                                        Property = "user.name"
                                    }
                                }
                            },
                            new SendActivity("Hello {user.name}, nice to meet you!")
                        }
                    }
                }
            };

            await CreateFlow(ruleDialog)
                .Send("hi")
                    .AssertReply("Hello, what is your name?")
                .Send("Carlos")
                    .AssertReply("Hello Carlos, nice to meet you!")
                .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_StringLiteralInExpression()
        {
            var ruleDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Events = new List<IOnEvent>()
                {
                    new OnUnknownIntent()
                    {
                        Actions = new List<IDialog>()
                        {
                            new IfCondition()
                            {
                                Condition = "user.name == null",
                                Actions = new List<IDialog>()
                                {
                                    new TextInput()
                                    {
                                        Prompt = new ActivityTemplate("Hello, what is your name?"),
                                        OutputBinding = "user.name"
                                    }
                                }
                            },
                            new IfCondition()
                            {
                                // Check comparison with string literal
                                Condition = "user.name == 'Carlos'",
                                Actions = new List<IDialog>()
                                {
                                    new SendActivity("Hello carlin")
                                }
                            },
                            new SendActivity("Hello {user.name}, nice to meet you!")
                        }
                    }
                }
            };

            await CreateFlow(ruleDialog)
            .Send(new Activity() { Type = ActivityTypes.ConversationUpdate, MembersAdded = new List<ChannelAccount>() { new ChannelAccount("bot", "Bot"), new ChannelAccount("user", "User") } })
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello carlin")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_DoActions()
        {
            var ruleDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "joke" },
                        { "HelloIntent", "hi|hello" }
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new IfCondition()
                            {
                                Condition = "user.name == null",
                                    Actions = new List<IDialog>()
                                    {
                                        new TextInput()
                                        {
                                            Prompt = new ActivityTemplate("Hello, what is your name?"),
                                            Property = "user.name"
                                        }
                                    }
                            },
                            new SendActivity("Hello {user.name}, nice to meet you!")
                        },
                    },
                    new OnIntent()
                    {
                        Intent="JokeIntent",
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("Why did the chicken cross the road?"),
                            new EndTurn(),
                            new SendActivity("To get to the other side")
                        }
                    },
                    new OnIntent()
                    {
                        Intent="HelloIntent",
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("Hello {user.name}, nice to meet you!")
                        }
                    }
                },
            };

            await CreateFlow(ruleDialog)
               .SendConversationUpdate()
                   .AssertReply("Hello, what is your name?")
               .Send("Carlos")
                   .AssertReply("Hello Carlos, nice to meet you!")
               .Send("Do you know a joke?")
                   .AssertReply("Why did the chicken cross the road?")
               .Send("Why?")
                   .AssertReply("To get to the other side")
               .Send("hi")
                   .AssertReply("Hello Carlos, nice to meet you!")
               .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_ReplacePlan()
        {
            var ruleDialog = new AdaptiveDialog("planningTest");
            ruleDialog.AutoEndDialog = false;
            ruleDialog.Recognizer = new RegexRecognizer()
            {
                Intents = new Dictionary<string, string>()
                {
                    { "JokeIntent", "(?i)joke" },
                    { "GreetingIntent", "(?i)greeting|hi|hello" }
                }
            };

            ruleDialog.AddEvents(new List<IOnEvent>()
            {
                new OnBeginDialog()
                {
                    Actions = new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Actions = new List<IDialog>()
                            {
                                new TextInput()
                                {
                                    Prompt = new ActivityTemplate("Hello, what is your name?"),
                                    Property = "user.name"
                                }
                            }
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    }
                },
                new OnIntent()
                {
                    Intent= "GreetingIntent",
                    Actions = new List<IDialog>()
                    {
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    }
                },
                new OnIntent("JokeIntent",
                    actions: new List<IDialog>()
                    {
                        new SendActivity("Why did the chicken cross the road?"),
                        new EndTurn(),
                        new SendActivity("To get to the other side")
                    }),
                new OnUnknownIntent(
                    actions: new List<IDialog>()
                    {
                        new SendActivity("I'm a joke bot. To get started say 'tell me a joke'")
                    })
            });

            await CreateFlow(ruleDialog)
            .SendConversationUpdate()
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .Send("hi")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_NestedInlineSequences()
        {
            var ruleDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "joke"},
                        { "GreetingIntemt", "hi|hello"},
                        { "GoodbyeIntent", "bye|goodbye|seeya|see ya"},
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new IfCondition()
                            {
                                Condition = "user.name == null",
                                Actions = new List<IDialog>()
                                {
                                    new TextInput()
                                    {
                                        Prompt = new ActivityTemplate("Hello, what is your name?"),
                                        Property = "user.name"
                                    }
                                }
                            },
                            new SendActivity("Hello {user.name}, nice to meet you!"),
                        },
                    },
                    new OnIntent("GreetingIntemt",
                        actions: new List<IDialog>()
                        {
                            new SendActivity("Hello {user.name}, nice to meet you!"),
                        }),
                    new OnIntent("JokeIntent",
                        actions: new List<IDialog>()
                        {
                            new SendActivity("Why did the chicken cross the road?"),
                            new EndTurn(),
                            new SendActivity("To get to the other side")
                        }),
                    new OnIntent("GoodbyeIntent",
                        actions: new List<IDialog>()
                        {
                            new SendActivity("See you later aligator!"),
                            new EndDialog()
                        }),
                    new OnUnknownIntent(
                        new List<IDialog>()
                        {
                            new SendActivity("I'm a joke bot. To get started say 'tell me a joke'")
                        })
                }
            };

            await CreateFlow(ruleDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .Send("hi")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("ummm")
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
            .Send("Goodbye")
                .AssertReply("See you later aligator!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_BeginDialog()
        {
            var innerDialog = new AdaptiveDialog("innerDialog")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "(?i)joke"},
                        { "GreetingIntent", "(?i)hi|hello"},
                        { "GoodbyeIntent", "(?i)bye|goodbye|seeya|see ya"}
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new BeginDialog("Greeting"),
                            new SendActivity("I'm a joke bot. To get started say 'tell me a joke'"),
                        },
                    },

                    new OnIntent("JokeIntent",
                        actions: new List<IDialog>()
                        {
                            new BeginDialog("TellJokeDialog"),
                        }),

                    new OnIntent("GreetingIntent",
                        actions: new List<IDialog>()
                        {
                            new BeginDialog("Greeting"),
                        }),

                    new OnIntent("GoodbyeIntent",
                        actions: new List<IDialog>()
                        {
                            new SendActivity("See you later aligator!"),
                            new EndDialog()
                        }),

                    new OnUnknownIntent(actions: new List<IDialog>()
                        {
                            new SendActivity("Like I said, I'm a joke bot. To get started say 'tell me a joke'"),
                        }),
                }
            };

            innerDialog.AddDialog(new[] {
                new AdaptiveDialog("Greeting")
                {
                    Events = new List<IOnEvent>()
                    {
                        new OnBeginDialog()
                        {

                            Actions = new List<IDialog>()
                            {
                                new IfCondition()
                                {
                                    Condition = "user.name == null",
                                    Actions = new List<IDialog>()
                                    {
                                        new TextInput()
                                        {
                                            Prompt = new ActivityTemplate("Hello, what is your name?"),
                                            Property = "user.name"
                                        },
                                        new SendActivity("Hello {user.name}, nice to meet you!")
                                    },
                                    ElseActions = new List<IDialog>()
                                    {
                                        new SendActivity("Hello {user.name}, nice to see you again!")
                                    }
                                }
                            }
                        }
                    }
                },
                new AdaptiveDialog("TellJokeDialog")
                    {
                        Events = new List<IOnEvent>()
                        {
                            new OnBeginDialog()
                            {

                                Actions = new List<IDialog>()
                                {
                                    new SendActivity("Why did the chicken cross the road?"),
                                    new EndTurn(),
                                    new SendActivity("To get to the other side")
                                }
                            }
                        }
                    }
                });

            var outerDialog = new AdaptiveDialog("outer")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "BeginIntent", "(?i)begin" },
                        { "HelpIntent", "(?i)help" }
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("Hi, type 'begin' to start a dialog, type 'help' to get help.")
                        },
                    },
                    new OnIntent("BeginIntent")
                    {
                        Actions = new List<IDialog>()
                        {
                            new BeginDialog("innerDialog")
                        }
                    },
                    new OnIntent("HelpIntent")
                    {
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("help is coming")
                        }
                    },
                    new OnUnknownIntent()
                    {
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("Hi, type 'begin' to start a dialog, type 'help' to get help.")
                        }
                    },
                }
            };
            outerDialog.AddDialog(new List<IDialog>() { innerDialog });


            await CreateFlow(outerDialog)
            .Send("hi")
                .AssertReply("Hi, type 'begin' to start a dialog, type 'help' to get help.")
            .Send("begin")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
            .Send("tell me a joke")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .Send("hi")
                .AssertReply("Hello Carlos, nice to see you again!")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .Send("ummm")
                 .AssertReply("Like I said, I'm a joke bot. To get started say 'tell me a joke'")
            .Send("help")
                .AssertReply("help is coming")
            .Send("bye")
                .AssertReply("See you later aligator!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_IntentEvent()
        {
            var planningDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "joke" }
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new SendActivity("I'm a joke bot. To get started say 'tell me a joke'")
                        },
                    },
                    new OnIntent("JokeIntent",
                        actions: new List<IDialog>()
                        {
                            new SendActivity("Why did the chicken cross the road?"),
                            new EndTurn(),
                            new SendActivity("To get to the other side")
                        }),
                }
            };

            await CreateFlow(planningDialog)
            .SendConversationUpdate()
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_NestedRecognizers()
        {
            var outerDialog = new AdaptiveDialog("outer")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "SideIntent", "side" },
                        { "CancelIntent", "cancel" },
                    }
                },

                Events = new List<IOnEvent>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<IDialog>()
                        {
                            new TextInput()
                            {
                                Prompt = new ActivityTemplate("name?"),
                                Property = "user.name"
                            },
                            new SendActivity("{user.name}"),
                            new NumberInput()
                            {
                                Prompt = new ActivityTemplate("age?"),
                                Property = "user.age"
                            },
                            new SendActivity("{user.age}"),
                        }
                    },
                    new OnIntent("SideIntent") { Actions = new List<IDialog>() { new SendActivity("sideintent") } },
                    new OnIntent("CancelIntent") { Actions = new List<IDialog>() { new EndDialog() } },
                    new OnUnknownIntent() { Actions = new List<IDialog>() { new SendActivity("outerWhat") } }
                }
            };

            var ruleDialog = new AdaptiveDialog("root")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "StartOuterIntent", "start" },
                        { "RootIntent", "root" },
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnIntent("StartOuterIntent", actions: new List<IDialog>() { outerDialog }),
                    new OnIntent("RootIntent", actions: new List<IDialog>() { new SendActivity("rootintent") }),
                    new OnUnknownIntent( new List<IDialog>() { new SendActivity("rootunknown") })
                }
            };

            await CreateFlow(ruleDialog)
            .Send("start")
                .AssertReply("name?")
            .Send("side")
                .AssertReply("sideintent")
                .AssertReply("name?")
            .Send("root")
                .AssertReply("rootintent")
                .AssertReply("name?")
            .Send("Carlos")
                .AssertReply("Carlos")
                .AssertReply("age?")
            .Send("root")
                .AssertReply("rootintent")
                .AssertReply("age?")
            .Send("side")
                .AssertReply("sideintent")
                .AssertReply("age?")
            .Send("10")
                .AssertReply("10")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_ActivityEvents()
        {
            var dialog = new AdaptiveDialog("test")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "joke" }
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnActivity("Custom", actions: new List<IDialog>() { new SendActivity("CustomActivityEvent") }),
                    new OnMessageActivity(actions: new List<IDialog>() { new SendActivity("MessageActivityEvent") }),
                    new OnMessageDeleteActivity(actions: new List<IDialog>() { new SendActivity("MessageDeleteActivityEvent") }),
                    new OnMessageUpdateActivity(actions: new List<IDialog>() { new SendActivity("MessageUpdateActivityEvent") }),
                    new OnMessageReactionActivity(actions: new List<IDialog>() { new SendActivity("MessageReactionActivityEvent") }),
                    new OnConversationUpdateActivity(actions: new List<IDialog>() { new SendActivity("ConversationUpdateActivityEvent") }),
                    new OnEndOfConversationActivity(actions: new List<IDialog>() { new SendActivity("EndOfConversationActivityEvent") }),
                    new OnInvokeActivity(actions: new List<IDialog>() { new SendActivity("InvokeActivityEvent") }),
                    new OnEventActivity(actions: new List<IDialog>() { new SendActivity("EventActivityEvent") }),
                    new OnHandoffActivity(actions: new List<IDialog>() { new SendActivity("HandoffActivityEvent") }),
                    new OnTypingActivity(actions: new List<IDialog>() { new SendActivity("TypingActivityEvent") }),
                    new OnMessageActivity(constraint: "turn.activity.text == 'constraint'", actions: new List<IDialog>() { new SendActivity("constraint") }),
                }
            };

            await CreateFlow(dialog)
            .SendConversationUpdate()
                .AssertReply("ConversationUpdateActivityEvent")
            .Send("MessageActivityEvent")
                .AssertReply("MessageActivityEvent")
            .Send("constraint")
                .AssertReply("constraint")
            .Send(new Activity(type: ActivityTypes.MessageUpdate))
                .AssertReply("MessageUpdateActivityEvent")
            .Send(new Activity(type: ActivityTypes.MessageDelete))
                .AssertReply("MessageDeleteActivityEvent")
            .Send(new Activity(type: ActivityTypes.MessageReaction))
                .AssertReply("MessageReactionActivityEvent")
            .Send(Activity.CreateTypingActivity())
                .AssertReply("TypingActivityEvent")
            .Send(Activity.CreateEndOfConversationActivity())
                .AssertReply("EndOfConversationActivityEvent")
            .Send(Activity.CreateEventActivity())
                .AssertReply("EventActivityEvent")
            .Send(Activity.CreateHandoffActivity())
                .AssertReply("HandoffActivityEvent")
            .Send(Activity.CreateInvokeActivity())
                .AssertReply("InvokeActivityEvent")
            .Send(new Activity(type: "Custom"))
                .AssertReply("CustomActivityEvent")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task AdaptiveDialog_ActivityAndIntentEvents()
        {
            var dialog = new AdaptiveDialog("test")
            {
                AutoEndDialog = false,
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>()
                    {
                        { "JokeIntent", "joke" }
                    }
                },
                Events = new List<IOnEvent>()
                {
                    new OnIntent(intent: "JokeIntent", actions: new List<IDialog>() { new SendActivity("chicken joke") }),
                    new OnMessageActivity(constraint: "turn.activity.text == 'magic'", actions: new List<IDialog>() { new SendActivity("abracadabra") }),
                }
            };

            await CreateFlow(dialog)
            .Send("tell me a joke")
                .AssertReply("chicken joke")
            .Send("magic")
                .AssertReply("abracadabra")
            .StartTestAsync();
        }

    }
}
