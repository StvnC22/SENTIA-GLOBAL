using AnalisisSentimiento.Application.Common.Models;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.Common.Models;

public class PaginatedListTests
{
    [Test]
    public void ShouldComputeTotalPagesFromCountAndPageSize()
    {
        var list = new PaginatedList<int>([1, 2, 3], totalCount: 25, pageNumber: 1, pageSize: 10);

        list.TotalPages.ShouldBe(3);
    }

    [Test]
    public void ShouldHaveNoPreviousPageOnFirstPage()
    {
        var list = new PaginatedList<int>([1, 2, 3], totalCount: 25, pageNumber: 1, pageSize: 10);

        list.HasPreviousPage.ShouldBeFalse();
        list.HasNextPage.ShouldBeTrue();
    }

    [Test]
    public void ShouldHaveNoNextPageOnLastPage()
    {
        var list = new PaginatedList<int>([1, 2, 3], totalCount: 25, pageNumber: 3, pageSize: 10);

        list.HasPreviousPage.ShouldBeTrue();
        list.HasNextPage.ShouldBeFalse();
    }

    [Test]
    public void ShouldExposeTheGivenItemsAndTotalCount()
    {
        var list = new PaginatedList<int>([1, 2, 3], totalCount: 25, pageNumber: 1, pageSize: 10);

        list.Items.ShouldBe([1, 2, 3]);
        list.TotalCount.ShouldBe(25);
        list.PageNumber.ShouldBe(1);
    }
}
