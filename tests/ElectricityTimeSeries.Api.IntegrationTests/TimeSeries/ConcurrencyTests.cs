using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

/// <summary>Races matter most on SQL Server, where requests really run at once. Each is repeated to give it a chance.</summary>
public class ConcurrencyTests : TimeSeriesApiTest
{
    private const int Rounds = 5;
    private const int Parallel = 10;

    public ConcurrencyTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task Parallel_deletes_of_one_series_give_one_204_and_otherwise_404()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var created = await Create(Series(points: 4));

            var responses = await Task.WhenAll(Enumerable.Range(0, Parallel).Select(_ => Delete(created.Id)));

            var codes = responses.Select(r => r.StatusCode).ToList();
            Assert.Single(codes, HttpStatusCode.NoContent);
            Assert.All(codes.Where(c => c != HttpStatusCode.NoContent), c => Assert.Equal(HttpStatusCode.NotFound, c));
            Assert.Equal(HttpStatusCode.NotFound, (await GetById(created.Id)).StatusCode);
        }
    }

    [Fact]
    public async Task Updates_racing_a_delete_never_fail_and_the_outcome_matches_the_delete_answer()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var created = await Create(Series(points: 4));

            var updates = Enumerable.Range(0, Parallel).Select(i => Put(created.Id, Version(i))).ToList();
            var delete = Delete(created.Id);
            var responses = await Task.WhenAll([.. updates, delete]);

            Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Conflict }));

            // The delete gets 409 if an update was saved first; then the series stays.
            var deleteAnswer = (await delete).StatusCode;
            var stored = await GetById(created.Id);
            if (deleteAnswer == HttpStatusCode.NoContent)
            {
                Assert.Equal(HttpStatusCode.NotFound, stored.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, deleteAnswer);
                Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Parallel_updates_leave_exactly_one_complete_version()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var created = await Create(Series(points: 4));
            var versions = Enumerable.Range(0, Parallel).Select(Version).ToArray();

            var responses = await Task.WhenAll(versions.Select(v => Put(created.Id, v)));

            Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

            var stored = await Get(created.Id);
            var winners = new List<SeriesResult>();
            foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
            {
                winners.Add(await ReadSeries(response));
            }

            Assert.Contains(winners, w => w.BiddingZone == stored.BiddingZone && w.End == stored.End && w.Points.SequenceEqual(stored.Points));
            Assert.Equal(stored.Points[^1].Timestamp + TimeSpan.FromMinutes(15), stored.End);
        }
    }

    [Fact]
    public async Task Parallel_creates_all_succeed()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, Parallel * 2).Select(i => Post(Version(i))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            ids.Add((await ReadSeries(response)).Id);
        }

        Assert.Equal(Parallel * 2, ids.Count);
        Assert.Equal(ids.Order(), (await Search()).Select(s => s.Id).Order());
    }

    [Fact]
    public async Task Conflict_responses_are_problem_details()
    {
        // Run updates until one is turned away, so its response can be checked.
        for (var round = 0; round < Rounds * 4; round++)
        {
            var created = await Create(Series(points: 4));
            var responses = await Task.WhenAll(Enumerable.Range(0, Parallel).Select(i => Put(created.Id, Version(i))));
            var conflict = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.Conflict);

            if (conflict is not null)
            {
                Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
                var problem = await conflict.Content.ReadFromJsonAsync<JsonObject>(Ct);
                Assert.Equal(409, problem!["status"]!.GetValue<int>());
                return;
            }
        }

        Assert.Skip("No update was turned away in these rounds, so there was no conflict response to check.");
    }

    /// <summary>A distinct, valid series body for each <paramref name="i"/>.</summary>
    private static JsonObject Version(int i)
    {
        string[] zones = ["NO1", "NO2", "NO3", "NO4", "NO5", "SE1", "SE2", "SE3", "SE4", "FI"];
        return Series(zone: zones[i % zones.Length], points: i + 1, value: 100m * i);
    }
}
