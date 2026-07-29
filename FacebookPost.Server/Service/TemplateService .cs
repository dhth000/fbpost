using System.Text.Json;
using FacebookPost.Server.Model;

namespace FacebookPost.Server.Service
{
    public class TemplateService : ITemplateService
    {
        private readonly List<FacebookTemplate> _templates;

        public TemplateService()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Template",
                "facebook-post-templates-v2.json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"Không tìm thấy file template: {path}");

            var json = File.ReadAllText(path);

            _templates = JsonSerializer.Deserialize<List<FacebookTemplate>>(json)
                         ?? throw new Exception("Không thể đọc template.");

            _templates = _templates
                .Where(x => x.IsActive)
                .OrderBy(x => x.Priority)
                .ToList();

            if (_templates.Count == 0)
                throw new Exception("Không có template nào được kích hoạt.");
        }

        public FacebookTemplate GetRandomTemplate()
        {
            return _templates[Random.Shared.Next(_templates.Count)];
        }
    }
}
