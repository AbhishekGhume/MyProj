using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Exceptions;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Logging;
using XmlMiddleware.Application.Models;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Infrastructure.Persistence;

public class OrderPersistenceService
    : IOrderPersistenceService
{
    private readonly ILogger<OrderPersistenceService> _logger;
    private readonly ApplicationDbContext _context;

    public OrderPersistenceService(
        ApplicationDbContext context,
        ILogger<OrderPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PersistOrdersResult> PersistOrdersAsync(
        long batchId,
        Guid correlationId,
        string fileName,
        string? fileHash,
        IReadOnlyCollection<CanonicalOrderModel> orders,
        CancellationToken cancellationToken = default)
    {
        void Log(
            LogLevel level,
            string message,
            string? error = null,
            Exception? exception = null) =>
            _logger.LogProcessing(
                level,
                correlationId,
                batchId,
                fileName,
                message,
                error,
                exception);

        // ------------------------------------------------------------
        // Idempotency: never write the same file's data twice
        // ------------------------------------------------------------

        var alreadyPersisted =
            await _context.Orders
                .AnyAsync(
                    x => x.BatchId == batchId,
                    cancellationToken);

        if (alreadyPersisted)
        {
            Log(
                LogLevel.Information,
                "DB: orders already persisted for this batch - skipping insert");

            return PersistOrdersResult.SkippedBecause(
                "Orders were already persisted for this batch.");
        }

        if (!string.IsNullOrEmpty(fileHash))
        {
            var persistedByEarlierBatch =
                await _context.Orders
                    .AnyAsync(
                        x => x.BatchId != batchId &&
                             x.Batch.FileHash == fileHash,
                        cancellationToken);

            if (persistedByEarlierBatch)
            {
                Log(
                    LogLevel.Information,
                    "DB: identical file content was already persisted by an earlier batch - skipping insert");

                return PersistOrdersResult.SkippedBecause(
                    "Identical file content was already persisted by an earlier batch.");
            }
        }

        if (orders.Count == 0)
        {
            Log(
                LogLevel.Warning,
                "DB: no orders to persist");

            return PersistOrdersResult.SkippedBecause(
                "The file contains no orders.");
        }

        var result = new PersistOrdersResult();

        Log(
            LogLevel.Information,
            $"DB: saving {orders.Count} order(s).");

        foreach (var orderModel in orders)
        {
            var customersBefore = result.CustomersInserted;
            var productsBefore = result.ProductsInserted;
            var linesBefore = result.OrderLinesInserted;

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var customer =
                    await GetOrCreateCustomerAsync(
                        orderModel,
                        result,
                        Log,
                        cancellationToken);

                var existingOrder =
                    await _context.Orders
                        .AnyAsync(
                            x => x.OrderId == orderModel.OrderId,
                            cancellationToken);

                if (existingOrder)
                {
                    throw new DataIntegrityException(
                        $"Order '{orderModel.OrderId}' already exists.");
                }

                var order =
                    new Order
                    {
                        OrderId = orderModel.OrderId,
                        BatchId = batchId,
                        CustomerSK = customer.CustomerSK,
                        OrderDate = DateOnly.FromDateTime(
                            orderModel.OrderDate),
                        AddressLine1 = orderModel.AddressLine1,
                        City = orderModel.City,
                        PostCode = orderModel.PostCode,
                        Country = orderModel.Country,
                        CreatedDate = DateTime.UtcNow
                    };

                _context.Orders.Add(order);

                await _context.SaveChangesAsync(
                    cancellationToken);

                var lineNumber = 1;

                foreach (var item in orderModel.Items)
                {
                    var product =
                        await GetOrCreateProductAsync(
                            item,
                            result,
                            Log,
                            cancellationToken);

                    _context.OrderDetails.Add(
                        new OrderDetail
                        {
                            OrderSK = order.OrderSK,
                            ProductSK = product.ProductSK,
                            LineNumber = lineNumber++,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice
                        });

                    result.OrderLinesInserted++;
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                result.OrdersInserted++;

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DataIntegrityException ex)
            {
                await RollbackQuietlyAsync(transaction);
                DetachOrderGraph();
                result.CustomersInserted = customersBefore;
                result.ProductsInserted = productsBefore;
                result.OrderLinesInserted = linesBefore;
                result.FailedOrders.Add(
                    new PersistOrderIssue(orderModel.OrderId, ex.Message));

                Log(
                    LogLevel.Warning,
                    $"DB: order rejected | OrderId={orderModel.OrderId} | Reason={ex.Message}",
                    exception: ex);
            }
            catch (Exception ex)
            {
                await RollbackQuietlyAsync(transaction);
                DetachOrderGraph();
                Log(LogLevel.Error, "DB: order transaction failed", exception: ex);
                throw;
            }
        }

        Log(
            LogLevel.Information,
            $"DB: Orders saved successfully | Orders={result.OrdersInserted} | FailedOrders={result.FailedOrders.Count} | NewCustomers={result.CustomersInserted} | NewProducts={result.ProductsInserted} | Lines={result.OrderLinesInserted}");

        return result;
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        CanonicalOrderModel orderModel,
        PersistOrdersResult result,
        Action<LogLevel, string, string?, Exception?> log,
        CancellationToken cancellationToken)
    {
        var customer =
            await _context.Customers
                .FirstOrDefaultAsync(
                    x => x.CustomerId == orderModel.CustomerId,
                    cancellationToken);

        if (customer is not null)
        {
            if (!string.Equals(
                    customer.FirstName,
                    orderModel.FirstName,
                    StringComparison.OrdinalIgnoreCase)
                ||
                !string.Equals(
                    customer.LastName,
                    orderModel.LastName,
                    StringComparison.OrdinalIgnoreCase)
                ||
                !string.Equals(
                    customer.Email,
                    orderModel.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                log(
                    LogLevel.Warning,
                    $"Customer data mismatch | CustomerId={customer.CustomerId} | ExistingName={customer.FirstName} {customer.LastName} | IncomingName={orderModel.FirstName} {orderModel.LastName} | ExistingEmail={customer.Email} | IncomingEmail={orderModel.Email}",
                    null,
                    null);

                throw new DataIntegrityException(
                    $"CustomerId '{orderModel.CustomerId}' already exists but incoming customer details differ from stored values.");
            }

            return customer;
        }

        // Customers.Email has a unique index - report a clash as a business
        // rejection instead of letting it surface as a database exception.
        var emailOwner =
            await _context.Customers
                .Where(x => x.Email == orderModel.Email)
                .Select(x => x.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

        if (emailOwner is not null)
        {
            throw new DataIntegrityException(
                $"Email '{orderModel.Email}' is already assigned to CustomerId '{emailOwner}'.");
        }

        customer = new Customer
        {
            CustomerId = orderModel.CustomerId,
            FirstName = orderModel.FirstName,
            LastName = orderModel.LastName,
            Email = orderModel.Email,
            CreatedDate = DateTime.UtcNow
        };

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync(
            cancellationToken);

        result.CustomersInserted++;

        return customer;
    }

    private async Task<Product> GetOrCreateProductAsync(
        CanonicalOrderItemModel item,
        PersistOrdersResult result,
        Action<LogLevel, string, string?, Exception?> log,
        CancellationToken cancellationToken)
    {
        var product =
            await _context.Products
                .FirstOrDefaultAsync(
                    x => x.ProductCode == item.ProductCode,
                    cancellationToken);

        if (product is not null)
        {
            if (!string.Equals(
                    product.ProductName,
                    item.ProductName,
                    StringComparison.OrdinalIgnoreCase))
            {
                log(
                    LogLevel.Warning,
                    $"Product data mismatch | ProductCode={item.ProductCode} | ExistingProductName={product.ProductName} | IncomingProductName={item.ProductName}",
                    null,
                    null);

                throw new DataIntegrityException(
                    $"ProductCode '{item.ProductCode}' already exists but incoming product details differ from stored values.");
            }

            return product;
        }

        product = new Product
        {
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            CreatedDate = DateTime.UtcNow
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync(
            cancellationToken);

        result.ProductsInserted++;

        return product;
    }

    private static async Task RollbackQuietlyAsync(
        IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(
                CancellationToken.None);
        }
        catch
        {
            // Never let a failed rollback hide the original exception.
        }
    }

    private void DetachOrderGraph()
    {
        var entries =
            _context.ChangeTracker
                .Entries()
                .Where(e =>
                    e.Entity is Customer
                        or Product
                        or Order
                        or OrderDetail)
                .ToList();

        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }
}