using FacebookPost.Server.Service;
using Microsoft.AspNetCore.Mvc;

namespace FacebookPost.Server.Controllers
{
    [ApiController]
    [Route("api/weatherForecast")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        private readonly IShopeeService _shopeeService;

        public WeatherForecastController(IShopeeService shopeeService)
        {
            _shopeeService = shopeeService;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            //var url = "https://shopee.vn/product/27495839/41570069594";
            //var cookie = "language=vi; ssr-tz=Asia/Saigon; _gcl_au=1.1.1218274249.1782834093; SPC_F=cUp1avFnUvMhNpPGn9dtsZLk617G6tJz; REC_T_ID=2abffcea-749a-11f1-9a17-0a921cbd1bba; _QPWSDCXHZQA=80659b21-daa8-4a30-96dc-0e5a57701f09; REC7iLP4Q=43d004bb-a9fb-444e-ad69-a047754b2cff; _ga=GA1.1.580301821.1782834096; _fbp=fb.1.1782834096141.747774247284159326; SPC_CLIENTID=Y1VwMWF2Rm5Vdk1oopowiyxljqsrlbye; _hjSessionUser_868286=eyJpZCI6IjE1ZjU1NWRkLWEzMmYtNThjOC1hOGQ0LWE5NjY5ZDlhMTU2YyIsImNyZWF0ZWQiOjE3ODI4MzQwOTYzMDQsImV4aXN0aW5nIjp0cnVlfQ==; _med=affiliates; _ga_QLZF8ZGF0S=GS2.1.s1784176307$o1$g1$t1784176654$j60$l0$h0; _ga_MEBV89P0VE=GS2.1.s1784176307$o1$g1$t1784176654$j60$l0$h0; SPC_SI=KDRLagAAAABEem1KOVQ4aiEa8gAAAAAAVFlUVGNZZkw=; SPC_SEC_SI=v1-bXpBS0plWEhkWVZYcWpFNN667g/dMSEcqjmpmhCOTzEtBd1u6FrXsG4ciNxbW2JQeGQSAoHX11DgDs6CVrzJM7rLaWcLZ7tCTPQ7Jkxmk2U=; SPC_ST=MWdZMGU5V0MzQ3kwaWxJUBfIf3tMawk7e1g7useJeDdvlvslFBFP9O4LpZ9xQ2Xg4+To7X0aM3Df+UlodsInBoKCfKDU8g0rLM6aRhn8zAM4doHncJG/mmhE5u6GOCc4JRu8CxwiEQqyRmMrJsKiyR2c1819E+7J4Fc/Df22AgsaOndi5jTOz94zaxs9kbTTyxhcFSH5ZJ3Edy8MERJcng==.AI7T381oBYnTBYUmZv4kbIQ8faFwRFwWHG17CJT5IxIa; SPC_U=271589408; SPC_R_T_ID=26nZd5ih1jIOqLHoJqNwP3Cq8/roG8kFx6vNc61pzmphTNk3dbjd6WepM7A4Sfq5W+cp8UJOH3LKOk0uyrZk8fEEmok2KfsIgAHrhHDDq86PD26Hm1P9v2KEbu5BugytAutBTFuJkIyimnTxGa+MEQFCyCC1IKFuSmrEJYaVh8E=; SPC_R_T_IV=NGJyM05qY3F1Y0E3Y1N4Vw==; SPC_T_ID=26nZd5ih1jIOqLHoJqNwP3Cq8/roG8kFx6vNc61pzmphTNk3dbjd6WepM7A4Sfq5W+cp8UJOH3LKOk0uyrZk8fEEmok2KfsIgAHrhHDDq86PD26Hm1P9v2KEbu5BugytAutBTFuJkIyimnTxGa+MEQFCyCC1IKFuSmrEJYaVh8E=; SPC_T_IV=NGJyM05qY3F1Y0E3Y1N4Vw==; _fbc=fb.1.1784615536260.IwY2xjawTL8bFleHRuA2FlbQIxMABicmlkETFraE5IdHZ4WGg2Wlp0Nmdjc3J0YwZhcHBfaWQQMjIyMDM5MTc4ODIwMDg5MgABHk0KwZVCHRrGnBZXasKAAja9AIipjGdfRbXFq1QyNRCC-9ev2sntZ9S2ht53_aem_DB6hGIJ9izG6enemSReO2g; _sapid=30ce1ff89c2d5c923593d3fa98e96fdce0ed5ad68de1ed32ed6dab15; csrftoken=PZxnx5pTVQjSIdQ0dlf0uIy3RyusTO3A; SPC_CDS_CHAT=82f68bf9-a7b8-4b4b-8455-31634b49fd91; SPC_IA=1; shopee_webUnique_ccd=sqCjnAkCeY%2BZmoTD6oaSTw%3D%3D%7Cw59SDB1sOr5OCyDN4dsJZQl6fOrL5BiFLysQNAGQs%2BC9ZheVSnCOaLvLuQCrdMYPdjtf%2BoTnwac%3D%7CDt%2BuppC3jWLnI7hn%7C08%7C3; ds=5b1b8ac9dbbea617c5b1ea75445b9e44; AC_CERT_D=gqRjZGVrxHeFomtpuDE0MjUxOmNhcHRjaGFfY29va2llX2tleaJrdtEAAaRhbGdv0gAAAGSjZGVrwKJjdMRAAAAADGE52xUo3Wm3iC5rRYtPIg498IF1KHCytnPhbjJaI6YqfVhynmwqpbv6ogr0FzEZ/3RtNpmV6B9rxJ+WP6pjaXBoZXJ0ZXh0xQNQAAAADIJfqoXiHaGWZS8ReveBV8nk3yqWoXKwcykSBmXl3EiNh8JvaZ8CQZ7/H2mmkx1mYkVsea8rgBUBXdaQviUPPf0Xeli+nKKwbvdvaUR5yudqVKPWueD0t6KvbpZ3SYvFjKerhEMH2+idpjs4u6pZuxjLWAldBtJQR3KdyP9wROkmOnSkujYtlSreCNL/1OCO6YvDszWboPLGZEw1z8I0nrFOEyqLsZ5xqwrsOKwV19LYlOyn00OBwDkfyUAWOBPI1VnPJNRqrT3jdxpIvVFRAEl58db6fEruo+1v7jDtnCoezH/HdcaUZiz4WM8t3tlXJpqTG3eNeg9BNCPJ+hMB6zORpKDuh9O7jvWPii7IrQqcuXUrsOIRPcGBpAJdM8/Irxq7fegQUxxPRyssOpYiPDkyGGwwp9wgOBD+BonbjWSNK2OZS8m3zj5BtMtgfRQwB6opi87DgurERwm72Lh1nyW5IMOmtwwopaJFGaQqCZ6cNViFTv5ZaIf4Be5wmWIQ96T0oAOotB55d2Xh7N5lbjR4fGRzBbLfNIenx3c0WxNMikZh9PS+xVC5loBcgOXGqVS1iA0ncYR+D/npaPoyRNecCCY/J9pJxuSxponmKZ6zb5pF2MTmRATKUdewRVKjUu/917ke4v1HZ9Cvx9grIMCqTYfBezTDXzwEdjK1tZt2W6DnTsp8/wR8Xj0rmvLcspDQbVZPkGFx6V5IzDcpcLsfrpqoNvkQ12zKVY03hRwutsbn314QHBM2gSNJRDf4+NpkrcseEdRP73fqGCuxvWPlRgQui23YmG+GO7CTJXQUj3lMVBfQmy+37wUIi3cRluvBaRJkiDwf/yMtd9N1RL8ap7tvG1tUY9dkKbf4P7MiMR7C9JBHmauKRix73aKFSdgbNQ8sQJOHpFBKcfJVS77OhNUHGNVezVN48ubHOEyPSwyTf1iRnoHvkLl8qNjU0NPOBQJR+Dw69Zc/0rTJbMKnXlw2Q7o8uJg/a41JNLi6T39jnYsTFLSrqYxUNqFK/ZbU2vVXjYaPpWWznCDHkROVnE627IsKSlC7LkRe1cPcHrLbxAsML+jfh0Wyin+gG3LQIVGH155glTvBNKUpV/ALWkwzUQb1Y1cRjrs=; _ga_4GPP1ZXG63=GS2.1.s1784644052$o60$g1$t1784644525$j32$l0$h126023710; sense_sa_r=s";
            //var json = await _shopeeService.GetProductJsonAsync(url);
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        //[HttpGet(Name = "UploadDataFileAsync")]
        //public async Task<IEnumerable<WeatherForecast>> UploadDataFileAsync()
        //{
        //    return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        //    {
        //        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        //        TemperatureC = Random.Shared.Next(-20, 55),
        //        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        //    })
        //    .ToArray();
        //}
    }
}
