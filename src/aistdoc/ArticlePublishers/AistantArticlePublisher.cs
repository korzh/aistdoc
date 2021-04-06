
using Microsoft.Extensions.Logging;

using Aistant.KbService;

namespace aistdoc
{
    internal class AistantArticlePublisher : IArticlePublisher 
    {
        private readonly AistantKbService _service;

        public AistantArticlePublisher(AistantSettings settings, ILogger logger ) 
        {
            _service = new AistantKbService(settings, logger);
        }

        public bool PublishArticle(ArticlePublishModel model)
        {
            return _service.UploadArticleAsync(model.SectionUri, model.SectionTitle, model.ArticleUri, model.ArticleTitle, model.ArticleBody, model.ArticleExcerpt, model.IsSection).Result;
        }
    }
}
