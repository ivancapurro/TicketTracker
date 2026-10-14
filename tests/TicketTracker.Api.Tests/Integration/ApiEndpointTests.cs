using System.Net;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Domain;
using TicketTracker.Api.Tests.Infrastructure;

namespace TicketTracker.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class SwaggerAndUserTests(TicketTrackerApiFactory factory)
{
    [Fact]
    public async Task Swagger_document_and_ui_are_available()
    {
        var client = factory.CreateClient();

        var document = await client.GetAsync("/swagger/v1/swagger.json");
        var json = await document.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Contains("Ticket Tracker API", json, StringComparison.Ordinal);
        Assert.Contains("/api/projects/{projectId}/tickets", json, StringComparison.Ordinal);
        Assert.Contains("/api/users", json, StringComparison.Ordinal);

        var ui = await client.GetAsync("/swagger/index.html");
        var html = await ui.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("swagger-ui", html, StringComparison.OrdinalIgnoreCase);

        var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("swagger-ui", await root.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Users_are_seeded_and_read_only()
    {
        var client = factory.CreateClient();
        var users = await (await client.GetAsync("/api/users")).ReadAsync<List<UserResponse>>();

        Assert.Equal(3, users.Count);
        Assert.Contains(users, user => user is { Name: "Ada Lovelace", Email: "ada@example.com" });
        Assert.Contains(users, user => user is { Name: "Grace Hopper", Email: "grace@example.com" });
        Assert.Contains(users, user => user is { Name: "Alan Turing", Email: "alan@example.com" });
        Assert.Equal(users.OrderBy(user => user.Name).Select(user => user.Id), users.Select(user => user.Id));
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/does-not-exist");
        var problem = await response.ReadProblemAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }
}

[Collection(ApiCollection.Name)]
public class ProjectEndpointTests(TicketTrackerApiFactory factory)
{
    [Fact]
    public async Task Crud_round_trip()
    {
        var client = factory.CreateClient();
        var key = ApiJson.NewProjectKey();

        var createdResponse = await client.PostJsonAsync("/api/projects", new CreateProjectRequest
        {
            Key = key,
            Name = "  Benchmark  ",
            Description = "  Notes  ",
        });
        var created = await createdResponse.ReadAsync<ProjectResponse>();
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(createdResponse.Headers.Location);
        Assert.EndsWith($"/api/projects/{created.Id}", createdResponse.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(key, created.Key);
        Assert.Equal("Benchmark", created.Name);
        Assert.Equal("Notes", created.Description);

        var fetched = await (await client.GetAsync($"/api/projects/{created.Id}")).ReadAsync<ProjectResponse>();
        Assert.Equal(created.Id, fetched.Id);

        var updated = await (await client.PutJsonAsync($"/api/projects/{created.Id}", new UpdateProjectRequest
        {
            Name = "Renamed",
            Description = null,
        })).ReadAsync<ProjectResponse>();
        Assert.Equal(key, updated.Key);
        Assert.Equal("Renamed", updated.Name);
        Assert.Null(updated.Description);

        var listed = await (await client.GetAsync("/api/projects")).ReadAsync<List<ProjectResponse>>();
        Assert.Contains(listed, project => project.Id == created.Id && project.Name == "Renamed");

        var deleted = await client.DeleteAsync($"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await client.GetAsync($"/api/projects/{created.Id}");
        var problem = await missing.ReadProblemAsync();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(404, problem.Status);
        Assert.Contains(created.Id.ToString(), problem.Detail, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Duplicate_key_returns_conflict_problem()
    {
        var client = factory.CreateClient();
        var key = ApiJson.NewProjectKey();
        await (await client.PostJsonAsync("/api/projects", new CreateProjectRequest { Key = key, Name = "One" })).ReadAsync<ProjectResponse>();

        var response = await client.PostJsonAsync("/api/projects", new CreateProjectRequest { Key = key, Name = "Two" });
        var problem = await response.ReadProblemAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Conflict", problem.Title);
        Assert.Contains(key, problem.Detail, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Invalid_project_returns_validation_problem()
    {
        var client = factory.CreateClient();
        var response = await client.PostJsonAsync("/api/projects", new { key = "tt", name = "   " });
        var problem = await response.ReadProblemAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem.Status);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.NotNull(problem.Errors);
        Assert.Equal(["key", "name"], problem.Errors!.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Delete_with_tickets_returns_conflict_until_tickets_are_removed()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var reporter = await UserAsync(client, "ada@example.com");
        var ticket = await CreateTicketAsync(client, project.Id, reporter.Id, "Blocker");

        var blocked = await client.DeleteAsync($"/api/projects/{project.Id}");
        var problem = await blocked.ReadProblemAsync();
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("still has tickets", problem.Detail, StringComparison.OrdinalIgnoreCase);

        var removed = await client.DeleteAsync($"/api/projects/{project.Id}/tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var deleted = await client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    internal static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostJsonAsync("/api/projects", new CreateProjectRequest
        {
            Key = ApiJson.NewProjectKey(),
            Name = name ?? "Project",
        });
        return await response.ReadAsync<ProjectResponse>();
    }

    internal static async Task<UserResponse> UserAsync(HttpClient client, string email)
    {
        var users = await (await client.GetAsync("/api/users")).ReadAsync<List<UserResponse>>();
        return users.Single(user => user.Email == email);
    }

    internal static async Task<TicketResponse> CreateTicketAsync(HttpClient client, int projectId, int reporterId, string title, int? assigneeId = null)
    {
        var response = await client.PostJsonAsync($"/api/projects/{projectId}/tickets", new CreateTicketRequest
        {
            Title = title,
            Description = "Details",
            Type = TicketType.Bug,
            Priority = TicketPriority.Medium,
            ReporterId = reporterId,
            AssigneeId = assigneeId,
        });
        return await response.ReadAsync<TicketResponse>();
    }
}

[Collection(ApiCollection.Name)]
public class TicketEndpointTests(TicketTrackerApiFactory factory)
{
    [Fact]
    public async Task Crud_round_trip_assigns_keys_inside_the_project()
    {
        var client = factory.CreateClient();
        var project = await ProjectEndpointTests.CreateProjectAsync(client);
        var ada = await ProjectEndpointTests.UserAsync(client, "ada@example.com");
        var grace = await ProjectEndpointTests.UserAsync(client, "grace@example.com");

        var createResponse = await client.PostJsonAsync($"/api/projects/{project.Id}/tickets", new CreateTicketRequest
        {
            Title = "  First bug  ",
            Type = TicketType.Bug,
            Priority = TicketPriority.High,
            ReporterId = grace.Id,
            AssigneeId = ada.Id,
        });
        var created = await createResponse.ReadAsync<TicketResponse>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.EndsWith($"/api/projects/{project.Id}/tickets/{created.Id}", createResponse.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal($"{project.Key}-1", created.Key);
        Assert.Equal("First bug", created.Title);
        Assert.Equal(TicketStatus.ToDo, created.Status);
        Assert.Equal(ada.Id, created.Assignee!.Id);
        Assert.Equal(grace.Id, created.Reporter.Id);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        var second = await ProjectEndpointTests.CreateTicketAsync(client, project.Id, grace.Id, "Second");
        Assert.Equal($"{project.Key}-2", second.Key);

        var fetched = await (await client.GetAsync($"/api/projects/{project.Id}/tickets/{created.Id}")).ReadAsync<TicketResponse>();
        Assert.Equal(created.Key, fetched.Key);

        var updated = await (await client.PutJsonAsync($"/api/projects/{project.Id}/tickets/{created.Id}", new UpdateTicketRequest
        {
            Title = "First bug",
            Description = "Now in progress",
            Type = TicketType.Bug,
            Priority = TicketPriority.Highest,
            Status = TicketStatus.InProgress,
            AssigneeId = ada.Id,
        })).ReadAsync<TicketResponse>();
        Assert.Equal(created.Key, updated.Key);
        Assert.Equal(TicketStatus.InProgress, updated.Status);
        Assert.Equal(TicketPriority.Highest, updated.Priority);
        Assert.Equal("Now in progress", updated.Description);

        var deleted = await client.DeleteAsync($"/api/projects/{project.Id}/tickets/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await client.GetAsync($"/api/projects/{project.Id}/tickets/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(404, (await missing.ReadProblemAsync()).Status);
    }

    [Fact]
    public async Task List_filters_by_status_and_assignee_and_pages()
    {
        var client = factory.CreateClient();
        var project = await ProjectEndpointTests.CreateProjectAsync(client);
        var ada = await ProjectEndpointTests.UserAsync(client, "ada@example.com");
        var grace = await ProjectEndpointTests.UserAsync(client, "grace@example.com");

        var first = await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "One", ada.Id);
        var second = await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "Two", grace.Id);
        await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "Three");
        await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "Four", ada.Id);
        await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "Five");

        await client.PutJsonAsync($"/api/projects/{project.Id}/tickets/{first.Id}", new UpdateTicketRequest
        {
            Title = first.Title,
            Type = first.Type,
            Priority = first.Priority,
            Status = TicketStatus.InProgress,
            AssigneeId = ada.Id,
        });

        var byStatus = await (await client.GetAsync($"/api/projects/{project.Id}/tickets?status=InProgress")).ReadAsync<PagedResult<TicketResponse>>();
        Assert.Equal(1, byStatus.TotalCount);
        Assert.Equal(first.Key, Assert.Single(byStatus.Items).Key);

        var byAssignee = await (await client.GetAsync($"/api/projects/{project.Id}/tickets?assigneeId={grace.Id}")).ReadAsync<PagedResult<TicketResponse>>();
        Assert.Equal(second.Key, Assert.Single(byAssignee.Items).Key);

        var page = await (await client.GetAsync($"/api/projects/{project.Id}/tickets?page=2&pageSize=2")).ReadAsync<PagedResult<TicketResponse>>();
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Page);
        Assert.Equal([$"{project.Key}-3", $"{project.Key}-4"], page.Items.Select(ticket => ticket.Key).ToArray());
    }

    [Fact]
    public async Task Invalid_references_and_queries_return_problem_details()
    {
        var client = factory.CreateClient();
        var project = await ProjectEndpointTests.CreateProjectAsync(client);
        var other = await ProjectEndpointTests.CreateProjectAsync(client);
        var ada = await ProjectEndpointTests.UserAsync(client, "ada@example.com");
        var ticket = await ProjectEndpointTests.CreateTicketAsync(client, project.Id, ada.Id, "Scoped");

        var missingProject = await client.PostJsonAsync("/api/projects/999999/tickets", new CreateTicketRequest
        {
            Title = "Ghost",
            Type = TicketType.Task,
            Priority = TicketPriority.Low,
            ReporterId = ada.Id,
        });
        Assert.Equal(HttpStatusCode.NotFound, missingProject.StatusCode);
        Assert.Contains("999999", (await missingProject.ReadProblemAsync()).Detail, StringComparison.Ordinal);

        var badAssignee = await client.PostJsonAsync($"/api/projects/{project.Id}/tickets", new CreateTicketRequest
        {
            Title = "Nobody",
            Type = TicketType.Task,
            Priority = TicketPriority.Low,
            ReporterId = ada.Id,
            AssigneeId = 999999,
        });
        var assigneeProblem = await badAssignee.ReadProblemAsync();
        Assert.Equal(HttpStatusCode.BadRequest, badAssignee.StatusCode);
        Assert.Contains("Assignee", assigneeProblem.Detail, StringComparison.Ordinal);

        var listed = await (await client.GetAsync($"/api/projects/{project.Id}/tickets")).ReadAsync<PagedResult<TicketResponse>>();
        Assert.DoesNotContain(listed.Items, item => item.Title == "Nobody");

        var wrongProject = await client.GetAsync($"/api/projects/{other.Id}/tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.NotFound, wrongProject.StatusCode);

        var badPage = await client.GetAsync($"/api/projects/{project.Id}/tickets?page=0&pageSize=500");
        var pageProblem = await badPage.ReadProblemAsync();
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        Assert.NotNull(pageProblem.Errors);
        Assert.Contains(pageProblem.Errors!.Keys, key => key.Contains("page", StringComparison.OrdinalIgnoreCase));

        var badStatus = await client.GetAsync($"/api/projects/{project.Id}/tickets?status=NotAStatus");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }
}
