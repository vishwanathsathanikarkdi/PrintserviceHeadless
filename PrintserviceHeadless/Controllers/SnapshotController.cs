using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using PuppeteerSharp;
using System.Text.Json;

namespace PrintserviceHeadless.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SnapshotController : ControllerBase
    {
        [HttpPost("screenshot")]
        public async Task<IActionResult> TakeScreenshot([FromBody] ScreenshotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest("URL is required.");

            //Get bearer token from request header
            //string accessToken = null;
            //if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            //{
            //    accessToken = authHeader.ToString().Replace("Bearer ", "");
            //}
            request.Url = "https://wells.kognif.ai/live/#/dashboard/67604c20-11e2-11ef-a77e-31164f79cadc";
            string accessToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSIsImtpZCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSJ9.eyJhdWQiOiI3ZGUyNDRiNS02ZDRiLTRmODAtYjZkYi1mYjU1MWZjMmY0MTAiLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC9iNTIyMjAwNi0xNWZlLTRhMTgtOTJlMi1lY2QwNDhmYWUyMzcvIiwiaWF0IjoxNzY4NDUzMjcwLCJuYmYiOjE3Njg0NTMyNzAsImV4cCI6MTc2ODQ1ODYxMywiYWNyIjoiMSIsImFpbyI6IkFjUUFPLzhhQUFBQWlFdnNBczFTS1RXTFNCeG9JVCtkQm9IM1BsYXV6ZithcDlKUTFFYlpQMEFYNWxKbWhoS0xDT3hXbWJ1cWlpL2dySTd4Vm1uUXIxTGxaUitha3crM3hKWHNxS0wrWWpjK1pUakdEaTk2djFuV0xiNmU5dFdFQXR1d3VTTkNLd1h3TnVQaGs5TGJWbHhoa3ZnWWFNdEJZaFVtdXNlWEdoY01yMjhwYUtrT2d0aGY1NnZ6SDh6WUhBeWVDelI3UDFNaUhCODQ0OVBsU3V4RkV1clZrT09SSVNwb2ZkcDQvd2ZiS0txbzBxVmxBU2FLdlM2MHBpYjNWdUM4UVhtVFRqNjQiLCJhbXIiOlsicnNhIl0sImFwcGlkIjoiN2RlMjQ0YjUtNmQ0Yi00ZjgwLWI2ZGItZmI1NTFmYzJmNDEwIiwiYXBwaWRhY3IiOiIwIiwiZW1haWwiOiJ2aXNod2FuYXRoLmF0aGFuaWthckBrb25nc2JlcmdkaWdpdGFsLmNvbSIsImlkcCI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0L2JmN2NiODcwLWIzNzgtNDJhYi1hNjE4LTMxNzA0YmMyZTliNy8iLCJpcGFkZHIiOiIxMDYuNTEuMjAwLjE5NiIsIm5hbWUiOiJWaXNod2FuYXRoIFN1ZGhpbmRyYSBBdGhhbmlrYXIiLCJvaWQiOiJmMGFkZjNiNC1jZWUwLTQzMTEtOTFmZS1iM2IzMzY2ZmUwOTkiLCJyaCI6IjEuQVVzQUJpQWl0ZjRWR0VxUzR1elFTUHJpTjdWRTRuMUxiWUJQdHR2N1ZSX0M5QkFaQWVCTEFBLiIsInNjcCI6IkFwcGxpY2F0aW9uLlJlYWRXcml0ZS5BbGwgR3JvdXBNZW1iZXIuUmVhZC5BbGwgVXNlci5SZWFkIFVzZXIuUmVhZEJhc2ljLkFsbCIsInNpZCI6IjAwOTlhZmQ5LThlOGQtNTVkMi1iMDYwLWZkYjExMmRmNzFkNCIsInN1YiI6IkVhVThpSzNiLU5vVnNheGYyc2ZCeTVwT3Z4QzBkc3lPeHRKRTVkS0IyYVEiLCJ0aWQiOiJiNTIyMjAwNi0xNWZlLTRhMTgtOTJlMi1lY2QwNDhmYWUyMzciLCJ1bmlxdWVfbmFtZSI6InZpc2h3YW5hdGguYXRoYW5pa2FyQGtvbmdzYmVyZ2RpZ2l0YWwuY29tIiwidXRpIjoiV1F2eVUxcjVMRWlmOVg0SE1MQUxBQSIsInZlciI6IjEuMCIsInhtc19mdGQiOiJDWFNwZERKRUkyR2JQS01QcUdFUmZhcUNvdmlvZ3VKWjV2SzFkLWlLY0xrQlpuSmhibU5sWXkxa2MyMXoifQ.TgwSevMB2P_qlMT0jvoENIjCZkFK6CeQajCohfB80SU6h1HUA-Vg_Uay0ewgJ2cxQfnxBeZL-YCiGUIrDZQnQZNuUgns-ZGsfW7x412x5S9pKI0dUNBgcc9MOJBb9iXeYwlQ6kEWF_Y6JoG25nBavzhyaLsXE3CCbTMvTtC9a_98eTp320VqRRVSn2eGbgZooIVhCCwbNjcVfSInmhvnCiSkoA8y7aOTJ15vYYtQpRifKocJG2D3LzmeOQkPCIv37-YGkpu5LkJ41glqLAf9_ZyJe93smbNNm5C-1_I14xX8KELZtVc68jMv93Vs2btMIl2Qr7Kyn1R-VIHHNkuxMQ";
            if (string.IsNullOrWhiteSpace(accessToken))
                return Unauthorized("Bearer token is missing.");

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                AcceptInsecureCerts = true,
                //    UserDataDir = @"C:\Users\VishwanathSudhindraA\AppData\Local\Google\Chrome\User Data",
                //Args = new[] { "--profile-directory=Default" }
                Args = new[] { "--profile-directory=Default", "--ignore-certificate-errors", "--start-maximized", "--disable-features=WebAuthentication", "--no-first-run", "--no-default-browser-check" }
            });

            await using var page = await browser.NewPageAsync();
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {accessToken}" }
            });
            //await page.GoToAsync("about:blank");

            //// The full JSON string, minified and properly escaped for JS
            //var oidcUserJson = @"{""id_token"":""eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSIsImtpZCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSJ9.eyJhdWQiOiI1ZjIyMWRhZS1mNzlhLTRhYmYtYTRlYS0yNDgxN2ZjMTkzZDYiLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC9kZWRjOGY1OC03N2M4LTQ4ZDQtYTQ5OS0wYzA3MTQzZmQ1ODYvIiwiaWF0IjoxNzY4Mzk0NTAwLCJuYmYiOjE3NjgzOTQ1MDAsImV4cCI6MTc2ODM5ODQwMCwiYW1yIjpbInJzYSJdLCJlbWFpbCI6InZpc2h3YW5hdGguYXRoYW5pa2FyQGtvbmdzYmVyZ2RpZ2l0YWwuY29tIiwiaWRwIjoiaHR0cHM6Ly9zdHMud2luZG93cy5uZXQvYmY3Y2I4NzAtYjM3OC00MmFiLWE2MTgtMzE3MDRiYzJlOWI3LyIsImlwYWRkciI6IjEwNi41MS4yMDAuMTk2IiwibmFtZSI6IlZpc2h3YW5hdGggU3VkaGluZHJhIEF0aGFuaWthciIsIm9pZCI6ImNhNmZjMDI3LTdmNmYtNGFkMi05OWRmLWIzYzgwZjliNjZiZSIsInJoIjoiMS5BWFFBV0lfYzNzaDMxRWlrbVF3SEZEX1ZocTRkSWwtYTk3OUtwT29rZ1hfQms5YmlBT0IwQUEuIiwic2lkIjoiMDA5OWFmZDktOGU4ZC01NWQyLWIwNjAtZmRiMTEyZGY3MWQ0Iiwic3ViIjoidm1qNDY2ZlVxWmdGbmhac0pGb1o3OEVsVUg1dXpzSDZZNUVRWUVIQnBfOCIsInRpZCI6ImRlZGM4ZjU4LTc3YzgtNDhkNC1hNDk5LTBjMDcxNDNmZDU4NiIsInVuaXF1ZV9uYW1lIjoidmlzaHdhbmF0aC5hdGhhbmlrYXJAa29uZ3NiZXJnZGlnaXRhbC5jb20iLCJ1dGkiOiJVT1ZvOWJkdXRFeUxoUmc5YUtTRUFBIiwidmVyIjoiMS4wIn0.Zu1QrWq95diXwFFRdxLr-Ud1xtka4e1vXwKDgQHsfWbrOG-0rhwrkBFTkVU9AME3gNeCwrQ0xS72Qdr04I5wGFWeITxam6fn4BK7m5_vjrDRq07LSx_-TNJcWUen5Qz0ADy6wwb3MOWVuhzvnydj5FRu7Bv4g66Dq2WVkecIHhhdrMspHOsJTGH5WSYZ4I0ECzW11kJPZ761j9hMYYEUfpVUWh1NB0CFAcEHWerO1uDyF69dnWe-ICwi8AM_lsHRM70Hf_Q9-mwVMC5cWGXkePIH1vSCr3MqeKykzGkPxQCqP3r4zIHfySf6DBvVWKBqTvDv3R38IovjvkKA7-_qHg"",""session_state"":""0099afd9 - 8e8d - 55d2 - b060 - fdb112df71d4"",""access_token"":""eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSIsImtpZCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSJ9.eyJhdWQiOiI1ZjIyMWRhZS1mNzlhLTRhYmYtYTRlYS0yNDgxN2ZjMTkzZDYiLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC9kZWRjOGY1OC03N2M4LTQ4ZDQtYTQ5OS0wYzA3MTQzZmQ1ODYvIiwiaWF0IjoxNzY4Mzk0NTAwLCJuYmYiOjE3NjgzOTQ1MDAsImV4cCI6MTc2ODM5OTc1NywiYWNyIjoiMSIsImFpbyI6IkFjUUFPLzhiQUFBQUJscHN5WW5GR0hVb01MRWsvaDh5RUJxSGphemMrMmk2NUVMazVQdjlIUXVLMjlTUWJSYWhuWFBNaXFkdFNLcUpPYzhTN3oxa00weExSanZjZXFEcHJFNCtNakVWYXZrZVBZOXdQNkxXK2ZBY0ZDR1JhbGtVVTlOamU4NnVQTk5DVzJJL3NEeklOYnNoUHFBTElYek02T2pvaFlkODNwR3ZxcjFUMS9pTFlDVlhFemJnUlMxdlN1M2dlbWEvaDN0VkhHSDJuQm1JTk5GcUdyYkFCSWRjbEJYTlJSS2xDZjJlMFVVcGZGUjBXQmIyT21uTThycEY4NG90WHpFbFhtdHUiLCJhbXIiOlsicnNhIl0sImFwcGlkIjoiNWYyMjFkYWUtZjc5YS00YWJmLWE0ZWEtMjQ4MTdmYzE5M2Q2IiwiYXBwaWRhY3IiOiIwIiwiZW1haWwiOiJ2aXNod2FuYXRoLmF0aGFuaWthckBrb25nc2JlcmdkaWdpdGFsLmNvbSIsImlkcCI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0L2JmN2NiODcwLWIzNzgtNDJhYi1hNjE4LTMxNzA0YmMyZTliNy8iLCJpcGFkZHIiOiIxMDYuNTEuMjAwLjE5NiIsIm5hbWUiOiJWaXNod2FuYXRoIFN1ZGhpbmRyYSBBdGhhbmlrYXIiLCJvaWQiOiJjYTZmYzAyNy03ZjZmLTRhZDItOTlkZi1iM2M4MGY5YjY2YmUiLCJyaCI6IjEuQVhRQVdJX2Mzc2gzMUVpa21Rd0hGRF9WaHE0ZElsLWE5NzlLcE9va2dYX0JrOWJpQU9CMEFBLiIsInNjcCI6IkFwcGxpY2F0aW9uLlJlYWRXcml0ZS5BbGwgR3JvdXBNZW1iZXIuUmVhZC5BbGwgVXNlci5SZWFkIFVzZXIuUmVhZEJhc2ljLkFsbCIsInNpZCI6IjAwOTlhZmQ5LThlOGQtNTVkMi1iMDYwLWZkYjExMmRmNzFkNCIsInN1YiI6InZtajQ2NmZVcVpnRm5oWnNKRm9aNzhFbFVINXV6c0g2WTVFUVlFSEJwXzgiLCJ0aWQiOiJkZWRjOGY1OC03N2M4LTQ4ZDQtYTQ5OS0wYzA3MTQzZmQ1ODYiLCJ1bmlxdWVfbmFtZSI6InZpc2h3YW5hdGguYXRoYW5pa2FyQGtvbmdzYmVyZ2RpZ2l0YWwuY29tIiwidXRpIjoiVU9WbzliZHV0RXlMaFJnOWFLU0VBQSIsInZlciI6IjEuMCIsInhtc19mdGQiOiI1ZURhSG1FZVpQeUR0RzVaMkNENVBROXZnNHFZZ1hQSEtxV2tXWEpNRjQ0QmMzZGxaR1Z1WXkxa2MyMXoifQ.e1esgikFxFxDwH34ArQjEXk5YE_FEjel-Rml0BWllOvKifckUTryOq23WXDHUJSoL9On5zUI7fveS5K4idcha1FTcjEsrCheAUY-6UfflnpnGrCFcMi2aubhL4An3eGOR1mZSxDJLaqTjfb0HtBzbj2jpXK16DXq8bgVrm45z_GKUqyfs2KTpHVxwC4qVE6FKBeP8ik59ScW7WYgh4PYSs0nd015NedcmZACczHEXwnQ1uSNTr-vE-gJfC8D1Q9Lt55g174WNx8ZajxCAlfCYtC_92biAYIPLgKnp9HL4uLyQ80JhYbJ9bqE-_fTgXCjmb5NsSlQbqQX3Vnj3PY1Yg"",""refresh_token"":""1.AXQAWI_c3sh31EikmQwHFD_Vhq4dIl - a979KpOokgX_Bk9biAOB0AA.BQABAwEAAAADAOz_BQD0_xfTrIxXZUwT1OSaTkl0ZfICsTBsnMm0xjZhxdvnYZTsSJ_ - 9a4qltk - oePW - wC8sJ0yBmIaO - Bze_gYHGT0iaRH8239fLDdUXZD7UvEmBssWuYN - 7TcEESpwMd0yAFxHwxMfUYjjFMsfJEHZ1EyUxKAyP42pNax5QnfoJCqGSzhcMJsFPZ__kbcbtr8WXT_Xiz9kzPe8Y1tTMdLMYVTo7cofRkTYke59eAXrBgmlp26oV9lNKSjtojHGnPov4LpY6h4Ev2ImAVZrheszdL6ft0oeM48l0iX5nMiGkg1lDKbuzUFAepBjoBzs115Vi8ZbjKnFz0KEa - bCaIRu3igBJqRig0NM6blfMS3nTF2u5bGLujYqR_Hj2itMxKhC4Azf44H3zZaLB2nJxSyDf3CoOZTxDSzzXWkfOIjG6tY4PcS - 2uH8KlDPrKpej_PiDPyk5GR2GyA2FM_g6QcMTShaENbvHjdl_Jq0ozjGMScv_ - QYXXs3bGU70I7jzQilrPpBdtiG9PPPejLadbbTQmV0g9OofWBP1hpCwajTcHdzgIO5YkHmG8NvgtTdRYBs2oc0ZY6ROAvd4bVbblkxzSXLC9fBBXatozpX9m4mDWc1dNiTFPUO7CpRL0i - 1TZ1vMq - sBQZHD8uQQ2M30Z3AYBkC5 - axbylEx1HztolzbFjCHd3O5DqTs3OXXQP8x - gWOSTxVvvik7pqnsdddZNw6pOc - WzWe_8EehQK7dY5fIPRiztQY9lY3zBEljryTCODJFyO8M5pP3GnzxxshQhzWXaUjL43MV3JdHdaZ7AbRrYyD_X - a6FkFOV4Rlu3pHE2a8O7xCQE2Y2_ftADyTL_ohLCCFa8DCf71CZelnrBuJ96RxF03gfBETjj8VhLzLizZvfHIfKEQLF4giZ0fRORDTfTzW4LtOq1aVRH7QvdVqcoVEvLSCVqvqFm86szFyNH2 - au7juy7l2llcF4lYzC_XjUzrtv - gA7AN9cM0Nw1e8dcB_s80x7gN - 3DnIf6UGrK88D5nrzS23zNTNkAOtsAtXjQIDdNg3SZbh2wEwd77gpBDq44832wFaxMEdN_CMKlDlnKzVpBOYe5VBJzjtJKRsEObT9Zc1VL8pqhfDfMI0zYLYoo1L2wlQhGWY80kgbfvt4JmHJkGQ4e1dbI4BupSqu00PiOpcXit8GkZ_2mITUcjVtKTCgRdvT - JYzUR92kMzr1ArW - 4MAsMx7Z3p4ilWTyapfZ_GfaxKtmM54WIbX0B7Me_Q8zJkdqD7KYRDmZslMcFwtZnRQsv6ra8rF4Enu - z2RdW8CwIBHNrf5tNmCPfs42q5SeZcRodrFsi6aclKYskB11sYEwfLVLWTIZNm2_MssRpxyaHubo2kwnQEC6KQmmQnZugSuAkZ3q_BDwPHSjfmjsYa_0Hy2NI94GU2A_r1aEGf0 - XzwOakxzZzjkMHg29vgmkQZjsLzysiIeOOSrFxN38f2vFO1boQ9uMfxwd9t8VmstxPhEyJeMGko6lEN1cTrOrXXfPuZIXwfw7YiYCy1J_zXnWwnX_fbOM0jqc5DVBtrZ2nfkXpkXFz3ZAb - JANzMO2irNHFLhLKnm3yqtM7pSYN81fgnBGKGwuvcJNJfHBkDEZOTx3Nxiumh11IiqbyjA_1O9PSHeeIcTfp_Nq1OxHC36chmUMjDGD7va0JZad3tBvokr"",""token_type"":""Bearer"",""scope"":""Application.ReadWrite.All GroupMember.Read.All User.Read User.ReadBasic.All"",""profile"":{""amr"":[""rsa""],""email"":""vishwanath.athanikar@kongsbergdigital.com"",""idp"":""https://sts.windows.net/bf7cb870-b378-42ab-a618-31704bc2e9b7/"",""ipaddr"":""106.51.200.196"",""name"":""Vishwanath Sudhindra Athanikar"",""oid"":""ca6fc027-7f6f-4ad2-99df-b3c80f9b66be"",""rh"":""1.AXQAWI_c3sh31EikmQwHFD_Vhq4dIl-a979KpOokgX_Bk9biAOB0AA."",""sid"":""0099afd9-8e8d-55d2-b060-fdb112df71d4"",""sub"":""vmj466fUqZgFnhZsJFoZ78ElUH5uzsH6Y5EQYEHBp_8"",""tid"":""dedc8f58-77c8-48d4-a499-0c07143fd586"",""unique_name"":""vishwanath.athanikar@kongsbergdigital.com"",""uti"":""UOVo9bdutEyLhRg9aKSEAA"",""ver"":""1.0""},""expires_at"":1768399756}";

            //// Set the OIDC user object in localStorage
            //await page.EvaluateExpressionAsync(
            //    "localStorage.setItem(" +
            //    "\"oidc.user:https://login.microsoftonline.com/dedc8f58-77c8-48d4-a499-0c07143fd586/:5f221dae-f79a-4abf-a4ea-24817fc193d6\"," +
            //    $"'{oidcUserJson}');"
            //);

            await page.GoToAsync(request.Url, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                Timeout = 40000
            });
            await Task.Delay(50000);
            var screenshotStream = await page.ScreenshotStreamAsync();

            return File(screenshotStream, "image/png");
        }

        [HttpPost("screenshot-playwright")]
        public async Task<IActionResult> TakeScreenshotPlayWright([FromBody] ScreenshotRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                    return BadRequest("URL is required.");

                //  request.Url = "https://localhost:5001/poseidonnext/live/#/dashboard/79beb216-8790-4c0f-addd-0513291ba570";
                request.Url = "https://localhost:5001/poseidonnext/live/#/dashboard/175ee150-9a96-11ee-90b9-1f37e01c1e42";
                string accessToken = GetAccessToken();
                if (string.IsNullOrWhiteSpace(accessToken))
                    return Unauthorized("Bearer token is missing.");

                using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                var browser = await LaunchBrowserAsync(playwright);
                var context = await CreateContextAsync(browser);
                var page = await context.NewPageAsync();

                await PerformSsoLoginAsync(page);
                await page.GotoAsync(request.Url, new Microsoft.Playwright.PageGotoOptions { WaitUntil = Microsoft.Playwright.WaitUntilState.NetworkIdle });
                await page.WaitForTimeoutAsync(50000);
                var tcs = new TaskCompletionSource();

                await page.ExposeFunctionAsync("notifyPlaywrightDone", () =>
                {
                    tcs.TrySetResult();
                });
                await page.EvaluateAsync("""(data) => localStorage.setItem('jobData', data)""", request.configuration);

                await page.EvaluateAsync("window.startBusinessJob()");

                await tcs.Task;

                await page.WaitForTimeoutAsync(50000);

                var screenshotBytes = await page.ScreenshotAsync(new Microsoft.Playwright.PageScreenshotOptions { FullPage = true });

                return File(screenshotBytes, "image/png", "screenshot.png");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private string GetAccessToken()
        {
            // Replace with your actual logic to retrieve the access token
            return "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSIsImtpZCI6IlBjWDk4R1g0MjBUMVg2c0JEa3poUW1xZ3dNVSJ9.eyJhdWQiOiI1ZjIyMWRhZS1mNzlhLTRhYmYtYTRlYS0yNDgxN2ZjMTkzZDYiLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC9kZWRjOGY1OC03N2M4LTQ4ZDQtYTQ5OS0wYzA3MTQzZmQ1ODYvIiwiaWF0IjoxNzY4NTM0MTYyLCJuYmYiOjE3Njg1MzQxNjIsImV4cCI6MTc2ODUzODk0OCwiYWNyIjoiMSIsImFpbyI6IkFjUUFPLzhiQUFBQURXVW0yY2R5TTJTdXYyNFVhYUVvV2NkMlRubDRwcFNWN0dueDd4R1pSaTZ4NWgzNFluWXBwcWN0dFdSQXZlaFJYS1FRK0ZUeVRrdDhyc1pFRGwwZ01RMGlkeEplQzlCQTFReDNla1pGampKTm1kTC82SlllblY4SHlFclJCR3d6dy9ZNm5NZDRmMjhNYXJRQWUxd3NmVTQrQmRwS1BvVmNJckFpaDVRSW1aenNZRXlWdkVxMWhhZGt1Skkxa1p5N2pPZWxZRFFudGxUN2pzbjRTZCtJZzRFdWtOWTBIWGU4YkU1RDg4SFBYSVZRMlo0OEtHTFRNK1RDTnFaSTNucUMiLCJhbXIiOlsicnNhIl0sImFwcGlkIjoiNWYyMjFkYWUtZjc5YS00YWJmLWE0ZWEtMjQ4MTdmYzE5M2Q2IiwiYXBwaWRhY3IiOiIwIiwiZW1haWwiOiJ2aXNod2FuYXRoLmF0aGFuaWthckBrb25nc2JlcmdkaWdpdGFsLmNvbSIsImlkcCI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0L2JmN2NiODcwLWIzNzgtNDJhYi1hNjE4LTMxNzA0YmMyZTliNy8iLCJpcGFkZHIiOiIxMDYuNTEuMjAwLjE5NiIsIm5hbWUiOiJWaXNod2FuYXRoIFN1ZGhpbmRyYSBBdGhhbmlrYXIiLCJvaWQiOiJjYTZmYzAyNy03ZjZmLTRhZDItOTlkZi1iM2M4MGY5YjY2YmUiLCJyaCI6IjEuQVhRQVdJX2Mzc2gzMUVpa21Rd0hGRF9WaHE0ZElsLWE5NzlLcE9va2dYX0JrOWJpQU9CMEFBLiIsInNjcCI6IkFwcGxpY2F0aW9uLlJlYWRXcml0ZS5BbGwgR3JvdXBNZW1iZXIuUmVhZC5BbGwgVXNlci5SZWFkIFVzZXIuUmVhZEJhc2ljLkFsbCIsInNpZCI6IjAwOTlhZmQ5LThlOGQtNTVkMi1iMDYwLWZkYjExMmRmNzFkNCIsInN1YiI6InZtajQ2NmZVcVpnRm5oWnNKRm9aNzhFbFVINXV6c0g2WTVFUVlFSEJwXzgiLCJ0aWQiOiJkZWRjOGY1OC03N2M4LTQ4ZDQtYTQ5OS0wYzA3MTQzZmQ1ODYiLCJ1bmlxdWVfbmFtZSI6InZpc2h3YW5hdGguYXRoYW5pa2FyQGtvbmdzYmVyZ2RpZ2l0YWwuY29tIiwidXRpIjoiVU9WbzliZHV0RXlMaFJnOWFLU0VBQSIsInZlciI6IjEuMCIsInhtc19mdGQiOiJOM2NRRGxLRVBCbnNVRTRTTGlLa2N6RkN6MHpZS0c5dGtzX3NIOFllbmlVQlpYVnliM0JsYm05eWRHZ3RaSE50Y3cifQ.TZSQ1Fxo5KSpQT63VrTCkTWR8wFivFIUHQDBGE2Y8iUH9xjBGHdT-jsdTXRtsXmhojtKUSJmvRPtqZ7ugb05S9P6nWxfo4LkUMtfAM1zj1wwc4I5C2VmEd7IPD4A5kUHivaYq7oqKplW7D5iwDpEW0_G2EL-w0aOImtbLKSXFuRhzZ8cEIA5rXZncWn-Pgiiuc72fDx0ZnpQnicre4cK5Ih_pqYvDqwnJKd0Cu3PrWdFUeEuDv-LS9xcATqswVNStcrXGgmzees-pFUjAyRQ1cz_rdFFUXrLGI50DNBShEfyv_p3-w5Altunmzhk1bZR-Xn-EpqLgFdgz8QqVLQ-GA";
        }

        private async Task<Microsoft.Playwright.IBrowser> LaunchBrowserAsync(IPlaywright playwright)
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--ignore-certificate-errors",
                    "--start-maximized",
                    "--disable-features=WebAuthentication",
                    "--no-first-run",
                    "--no-default-browser-check"
                }
            });
        }

        private async Task<Microsoft.Playwright.IBrowserContext> CreateContextAsync(Microsoft.Playwright.IBrowser browser)
        {
            return await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize() { Width = 1280, Height = 1024 }
            });
        }

        private async Task PerformSsoLoginAsync(Microsoft.Playwright.IPage page)
        {
            await page.GotoAsync("https://scaas-qa.kognif.ai/");

            var UserNameLoc = page.Locator("[placeholder='Email, phone, or Skype']");
            var NextLoc = page.Locator("text=Next");
            var PasswordLoc = page.Locator("[placeholder='Password']");
            var SignInButton = page.Locator("input:has-text('Sign in')");
            var YesButton = page.Locator("//input[@type='submit']");
            var userName = "automationUser3@scaasqa.onmicrosoft.com";
            var password = "Mumbai1@2025";

            await UserNameLoc.ClickAsync();
            await UserNameLoc.FillAsync(userName);
            await NextLoc.ClickAsync();
            await PasswordLoc.ClickAsync();
            await PasswordLoc.FillAsync(password);
            await SignInButton.ClickAsync();

            //    int c = await page.Locator("text=Do this to reduce the number of times you are asked to sign in.").CountAsync();
            int c = await page.Locator("#KmsiDescription").CountAsync();
            if (c == 1)
            {
                await YesButton.ClickAsync();
            }
        }
    }

    public class ScreenshotRequest
    {
        public string Url { get; set; }
        public string configuration { get; set; }
    }
}

