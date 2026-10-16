using Tcc.Api.DependencyInjection;
using Tcc.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBrowserCors(builder.Configuration);
builder.Services.AddIgdbAuthentication(builder.Configuration);
builder.Services.AddCatalog(builder.Configuration);
builder.Services.AddPostgresPersistence(builder.Environment);
builder.Services.AddIdentityFeature();
builder.Services.AddOpinionsFeature();
builder.Services.AddSocialFeature();
builder.Services.AddQueriesFeature();
builder.Services.AddApiDocumentation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApiHttp();

var app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();
app.Run();

public partial class Program;
