using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Services;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>Dapper-based data access for <see cref="Course"/>.</summary>
public sealed class CourseRepository : ICourseRepository
{
    private const string TableName = "Course";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    // Shared SELECT projection; FK display names come from flat JOIN aliases.
    private const string SelectColumns = @"
        c.pkid AS Pkid,
        c.Title AS Title,
        c.OfficialTitle AS OfficialTitle,
        c.CourseId AS CourseId,
        c.ProdCourseId AS ProdCourseId,
        c.FriendlyUrl AS FriendlyUrl,
        c.DisplayOrder AS DisplayOrder,
        c.Partner_pkid AS PartnerPkid,
        c.CourseGroup_pkid AS CourseGroupPkid,
        c.PublishStatus_pkid AS PublishStatusPkid,
        c.ScheduleOn AS ScheduleOn,
        c.ScheduleOff AS ScheduleOff,
        c.Hour AS Hour,
        c.ListPrice AS ListPrice,
        c.LearningCredit AS LearningCredit,
        c.Material AS Material,
        c.Objective AS Objective,
        c.Target AS Target,
        c.Prerequisites AS Prerequisites,
        c.Outline AS Outline,
        c.TowardCertOrExam AS TowardCertOrExam,
        c.Note AS Note,
        c.OtherInfo AS OtherInfo,
        c.CanRepeat AS CanRepeat,
        p.Name AS PartnerName,
        g.Description AS CourseGroupDescription,
        s.Description AS PublishStatusDescription";

    private const string FromJoins = @"
        FROM Course c
        JOIN Partner p ON p.pkid = c.Partner_pkid
        LEFT JOIN CourseGroup g ON g.pkid = c.CourseGroup_pkid
        JOIN PublishStatus s ON s.pkid = c.PublishStatus_pkid";

    public CourseRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"SELECT {SelectColumns} {FromJoins} ORDER BY c.DisplayOrder ASC;";
        var rows = await connection.QueryAsync<Course>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sql = $@"
            SELECT {SelectColumns}
            {FromJoins}
            WHERE (@Keyword IS NULL
                   OR c.Title LIKE '%' + @Keyword + '%' ESCAPE '\'
                   OR c.OfficialTitle LIKE '%' + @Keyword + '%' ESCAPE '\'
                   OR c.CourseId LIKE '%' + @Keyword + '%' ESCAPE '\'
                   OR c.ProdCourseId LIKE '%' + @Keyword + '%' ESCAPE '\'
                   OR c.FriendlyUrl LIKE '%' + @Keyword + '%' ESCAPE '\')
              AND (@PartnerPkid IS NULL OR c.Partner_pkid = @PartnerPkid)
              AND (@CourseGroupPkid IS NULL OR c.CourseGroup_pkid = @CourseGroupPkid)
              AND (@PublishStatusPkid IS NULL OR c.PublishStatus_pkid = @PublishStatusPkid)
              AND (@ScheduleOnFrom IS NULL OR c.ScheduleOn >= @ScheduleOnFrom)
              AND (@ScheduleOnTo IS NULL OR c.ScheduleOn <= @ScheduleOnTo)
              AND (@ScheduleOffFrom IS NULL OR c.ScheduleOff >= @ScheduleOffFrom)
              AND (@ScheduleOffTo IS NULL OR c.ScheduleOff <= @ScheduleOffTo)
              AND (@CanRepeat IS NULL OR c.CanRepeat = @CanRepeat)
            ORDER BY c.DisplayOrder ASC;";
        var parameters = new
        {
            Keyword = SqlLike.EscapeWildcards(string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()),
            query.PartnerPkid,
            query.CourseGroupPkid,
            query.PublishStatusPkid,
            query.ScheduleOnFrom,
            query.ScheduleOnTo,
            query.ScheduleOffFrom,
            query.ScheduleOffTo,
            query.CanRepeat,
        };
        var rows = await connection.QueryAsync<Course>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, transaction: null, pkid, cancellationToken);
    }

    // Transaction-aware read used by the mutating methods so the "before"/"after" audit snapshots
    // (including the N-N pkid lists) see uncommitted state on the same connection.
    private static async Task<Course?> GetByIdAsync(IDbConnection connection, IDbTransaction? transaction, int pkid, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT {SelectColumns} {FromJoins} WHERE c.pkid = @Pkid;
            SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid;
            SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid;";
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var course = await multi.ReadSingleOrDefaultAsync<Course>();
        if (course is null)
        {
            return null;
        }

        course.CertificationPkids = (await multi.ReadAsync<int>()).AsList();
        course.JobCategoryPkids = (await multi.ReadAsync<short>()).AsList();
        return course;
    }

    // pkid is int IDENTITY — assigned by the DB; read back via SCOPE_IDENTITY.
    public async Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO Course
                (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
                 Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff,
                 Hour, ListPrice, LearningCredit, Material, Objective, Target, Prerequisites,
                 Outline, TowardCertOrExam, Note, OtherInfo, CanRepeat)
            VALUES
                (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
                 @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff,
                 @Hour, @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites,
                 @Outline, @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            WriteParams(request), transaction, cancellationToken: cancellationToken));

        await ReplaceCertificationsAsync(connection, transaction, pkid, request.CertificationPkids, cancellationToken);
        await ReplaceJobCategoriesAsync(connection, transaction, pkid, request.JobCategoryPkids, cancellationToken);

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);
        await _auditWriter.LogInsertAsync(connection, transaction, TableName, created!, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        if (before is null)
        {
            transaction.Rollback();
            return false;
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE Course SET
                Title = @Title,
                OfficialTitle = @OfficialTitle,
                CourseId = @CourseId,
                ProdCourseId = @ProdCourseId,
                FriendlyUrl = @FriendlyUrl,
                DisplayOrder = @DisplayOrder,
                Partner_pkid = @PartnerPkid,
                CourseGroup_pkid = @CourseGroupPkid,
                PublishStatus_pkid = @PublishStatusPkid,
                ScheduleOn = @ScheduleOn,
                ScheduleOff = @ScheduleOff,
                Hour = @Hour,
                ListPrice = @ListPrice,
                LearningCredit = @LearningCredit,
                Material = @Material,
                Objective = @Objective,
                Target = @Target,
                Prerequisites = @Prerequisites,
                Outline = @Outline,
                TowardCertOrExam = @TowardCertOrExam,
                Note = @Note,
                OtherInfo = @OtherInfo,
                CanRepeat = @CanRepeat
            WHERE pkid = @Pkid;",
            WriteParams(request), transaction, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await ReplaceCertificationsAsync(connection, transaction, request.Pkid, request.CertificationPkids, cancellationToken);
        await ReplaceJobCategoriesAsync(connection, transaction, request.Pkid, request.JobCategoryPkids, cancellationToken);

        var after = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogUpdateAsync(connection, transaction, TableName, before, after!, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var row = await GetByIdAsync(connection, transaction, pkid, cancellationToken);
        if (row is null)
        {
            transaction.Rollback();
            return false;
        }

        // Remove junction rows first to satisfy the FK constraints.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid;",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid;",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Course WHERE pkid = @Pkid;",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await _auditWriter.LogDeleteAsync(connection, transaction, TableName, row, cancellationToken);

        transaction.Commit();
        return true;
    }

    // Named parameters shared by INSERT and UPDATE (Pkid is ignored by INSERT).
    private static object WriteParams(CourseRequest request) => new
    {
        request.Pkid,
        request.Title,
        request.OfficialTitle,
        request.CourseId,
        request.ProdCourseId,
        request.FriendlyUrl,
        request.DisplayOrder,
        request.PartnerPkid,
        request.CourseGroupPkid,
        request.PublishStatusPkid,
        request.ScheduleOn,
        request.ScheduleOff,
        request.Hour,
        request.ListPrice,
        request.LearningCredit,
        request.Material,
        request.Objective,
        request.Target,
        request.Prerequisites,
        request.Outline,
        request.TowardCertOrExam,
        request.Note,
        request.OtherInfo,
        request.CanRepeat,
    };

    // N-N sync: delete-then-reinsert the CourseInCertification rows for this course.
    private static async Task ReplaceCertificationsAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int coursePkid,
        IEnumerable<int> certificationPkids,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Course_pkid = @CoursePkid;",
            new { CoursePkid = coursePkid }, transaction, cancellationToken: cancellationToken));

        var distinct = certificationPkids.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@CoursePkid, @CertificationPkid);",
            distinct.Select(id => new { CoursePkid = coursePkid, CertificationPkid = id }),
            transaction, cancellationToken: cancellationToken));
    }

    // N-N sync: delete-then-reinsert the CourseJobCategories rows for this course.
    private static async Task ReplaceJobCategoriesAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int coursePkid,
        IEnumerable<short> jobCategoryPkids,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseJobCategories WHERE Course_pkid = @CoursePkid;",
            new { CoursePkid = coursePkid }, transaction, cancellationToken: cancellationToken));

        var distinct = jobCategoryPkids.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@CoursePkid, @JobCategoryPkid);",
            distinct.Select(id => new { CoursePkid = coursePkid, JobCategoryPkid = id }),
            transaction, cancellationToken: cancellationToken));
    }
}
