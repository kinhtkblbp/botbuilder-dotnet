﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Bot.Builder.Templates
{
    public class TemplateIdMap : Dictionary<string, Func<BotContext, dynamic, object>>
    {
    }

    public class TemplateDictionary : Dictionary<string, TemplateIdMap>
    {
    }

    /// <summary>
    ///   This is a simple template engine which has a resource map of template functions
    ///  let myTemplates  = {
    ///       "en" : {
    ///         "templateId": (context, data) => $"your name  is {data.name}",
    ///         "templateId": (context, data) => { return new Activity(); }
    ///     }`  
    ///  }
    ///  }
    ///   To use, simply add to your pipeline
    ///   bot.use(new DictionaryTemplateEngine(myTemplates))
    /// </summary>
    public class DictionaryTemplateEngine : ITemplateEngine, IContextCreated
    {
        private TemplateDictionary languages;

        public DictionaryTemplateEngine(TemplateDictionary templates)
        {
            this.languages = templates;
        }

        public async Task ContextCreated(BotContext context)
        {
            context.TemplateManager.Register(this);
        }

        public Task<object> RenderTemplate(BotContext context, string language, string templateId, object data)
        {
            if (this.languages.TryGetValue(language, out var templates))
            {
                if (templates.TryGetValue(templateId, out var template))
                {
                    dynamic result = template(context, data);
                    if (result != null)
                    {
                        return Task.FromResult(result as object);
                    }
                }
            }

            return Task.FromResult((object)null);
        }
    }
    public static class BotDictionaryTemplateExtensions
    {
        /// <summary>
        /// UseTemplates- Adds templateDictionary to templateManager
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="templates"></param>
        /// <returns></returns>
        public static Bot UseTemplates(this Bot bot, TemplateDictionary templates)
        {
            return bot.Use(new DictionaryTemplateEngine(templates));
        }

    }

}
