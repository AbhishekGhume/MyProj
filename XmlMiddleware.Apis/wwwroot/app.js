let selectedBatchId = null;
let allBatches = [];

loadHealth();
loadSummary();
loadBatches();

async function loadHealth() {

    const response =
        await fetch('/api/health');

    const data =
        await response.json();

    document.getElementById('healthSection').innerHTML =
        `
        <h2>Health Status</h2>

        <div class="card-container">

            <div class="card">
                <strong>Database</strong><br/>
                ${data.database
            ? "✅ Healthy"
            : "❌ Unhealthy"}
            </div>

            <div class="card">
                <strong>Blob Storage</strong><br/>
                ${data.blobStorage
            ? "✅ Healthy"
            : "❌ Unhealthy"}
            </div>

            <div class="card">
                <strong>Service Bus</strong><br/>
                ${data.serviceBus
            ? "✅ Healthy"
            : "❌ Unhealthy"}
            </div>

        </div>
        `;
}

async function loadSummary() {

    const response =
        await fetch('/api/dashboard');

    const data =
        await response.json();

    document.getElementById('summarySection').innerHTML =
        `
        <h2>Dashboard Summary</h2>

        <div class="card-container">

            <div class="card">
                <strong>Total Files</strong><br/>
                ${data.totalFiles}
            </div>

            <div class="card">
                <strong>Completed</strong><br/>
                ${data.completed}
            </div>

            <div class="card">
                <strong>Failed</strong><br/>
                ${data.failed}
            </div>

            <div class="card">
                <strong>Processing</strong><br/>
                ${data.processing}
            </div>

        </div>
        `;
}

async function loadBatches() {

    const response =
        await fetch('/api/batches');

    const batches =
        await response.json();

    allBatches = batches;

    // const tbody =
    //     document.querySelector(
    //         '#batchTable tbody');

    // tbody.innerHTML = '';

    renderBatches(batches);
}

async function loadBatchDetails(batchId) {

    const detailsSection =
        document.getElementById('detailsSection');

    // If same row clicked again => collapse
    if (selectedBatchId === batchId) {

        detailsSection.style.display = 'none';

        selectedBatchId = null;

        return;
    }

    selectedBatchId = batchId;

    detailsSection.style.display = 'block';

    const response =
        await fetch(`/api/batches/${batchId}`);

    const batch =
        await response.json();

    document.getElementById('batchDetails')
        .innerHTML =
        `
        <div class="card">

            <h3>${batch.fileName}</h3>

            <p>
                <strong>Batch Id:</strong>
                ${batch.batchId}
            </p>

            <p>
                <strong>Correlation Id:</strong>
                ${batch.correlationId}
            </p>

            <p>
                <strong>Status:</strong>
                ${batch.status}
            </p>

            <p>
                <strong>Retry Count:</strong>
                ${batch.retryCount}
            </p>

            <p>
                <strong>Current Step:</strong>
                ${batch.currentStep}
            </p>

        </div>
        `;

    await loadJourney(batchId);

    await loadOutputs(batchId);
}

function renderBatches(batches) {

    const tbody =
        document.querySelector(
            '#batchTable tbody');

    tbody.innerHTML = '';

    batches.forEach(batch => {

        let statusClass = '';

        if (batch.status === 'Completed')
            statusClass = 'success';

        if (batch.status === 'Failed')
            statusClass = 'failed';

        if (batch.status === 'Processing')
            statusClass = 'processing';

        const row =
            `
        <tr onclick="loadBatchDetails(${batch.batchId})">

            <td>${batch.batchId}</td>

            <td>${batch.fileName}</td>

            <td class="${statusClass}">
                ${batch.status}
            </td>

            <td>${batch.retryCount}</td>

        </tr>
        `;

        tbody.innerHTML += row;
    });
}

function applyFilters() {

    const status =
        document.getElementById(
            'statusFilter').value;

    const searchText =
        document.getElementById(
            'searchBox')
            .value
            .toLowerCase();

    let filtered =
        allBatches;

    if (status !== 'All') {
        filtered =
            filtered.filter(x =>
                x.status === status);
    }

    if (searchText) {
        filtered =
            filtered.filter(x =>

                x.batchId
                    .toString()
                    .includes(searchText)

                ||

                x.fileName
                    ?.toLowerCase()
                    .includes(searchText)

                ||

                x.correlationId
                    ?.toLowerCase()
                    .includes(searchText)
            );
    }

    renderBatches(filtered);
}

async function loadJourney(batchId) {

    const response =
        await fetch(`/api/batches/${batchId}/journey`);

    const events =
        await response.json();

    let html =
        '<div class="timeline-container">';

    events.forEach(event => {

        const date =
            new Date(
                event.createdDateTime)
                .toLocaleString();

        html +=
            `
        <div class="timeline-event">

            <div class="timeline-title">
                ${event.eventType}
            </div>

            <div class="timeline-message">
                ${event.message}
            </div>

            <div class="timeline-date">
                ${date}
            </div>

        </div>
        `;
    });

    html += '</div>';

    document.getElementById(
        'journeySection')
        .innerHTML = html;
}

async function loadOutputs(batchId) {

    const response =
        await fetch(`/api/batches/${batchId}/outputs`);

    const outputs =
        await response.json();

    let html =
        `
    <table>

        <thead>
            <tr>
                <th>Output Type</th>
                <th>Status</th>
                <th>Attempts</th>
                <th>Path</th>
            </tr>
        </thead>

        <tbody>
    `;

    outputs.forEach(output => {

        html +=
            `
        <tr>

            <td>
                ${output.outputType}
            </td>

            <td>
                ${output.status}
            </td>

            <td>
                ${output.attemptCount}
            </td>

            <td>
                ${output.outputBlobPath}
            </td>

        </tr>
        `;
    });

    html +=
        `
        </tbody>

    </table>
    `;

    document.getElementById('outputSection')
        .innerHTML = html;
}