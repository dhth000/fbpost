using FacebookPost.Server.Service;
using Quartz;

namespace FacebookPost.Server.SchedulerJobs
{
    public class PublishAllFacebookJob : IJob
    {
        private readonly IFacebookService _facebookService;

        public PublishAllFacebookJob(
            IFacebookService facebookService)
        {
            _facebookService = facebookService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await _facebookService.ProcessCreatePostToAllFacebookPages();
        }
    }
}
