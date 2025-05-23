$(document).ready(function () {
    loadDataTable();
});

function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        ajax: { url: '/admin/product/getall' },
        "processing": true,
        "language": {
            "processing": '<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>'
        },
        columns: [
            { 
                data: 'title',
                width: '25%',
                render: function(data, type, row) {
                    return `<div class="d-flex align-items-center">
                                <img src="${row.productImages && row.productImages[0] ? row.productImages[0].imageUrl : '/images/no-image.png'}" 
                                     class="rounded-3 me-3" style="width: 50px; height: 50px; object-fit: cover;">
                                <div>
                                    <div class="fw-bold">${data}</div>
                                    <div class="text-muted small">${row.description ? row.description.substring(0, 50) + '...' : ''}</div>
                                </div>
                            </div>`;
                }
            },
            { 
                data: 'isbn',
                width: '15%',
                render: function(data) {
                    return `<span class="badge bg-light text-dark">${data}</span>`;
                }
            },
            { 
                data: 'listPrice',
                width: '10%',
                render: function(data) {
                    return `<span class="fw-bold">${formatPriceVND(data)}</span>`;
                }
            },
            { 
                data: 'author',
                width: '15%'
            },
            { 
                data: 'category.name',
                width: '10%',
                render: function(data) {
                    return `<span class="badge bg-info">${data}</span>`;
                }
            },
            {
                data: 'id',
                width: '10%',
                render: function (data) {
                    return `<div class="w-75 btn-group" role="group">
                        <a href="/admin/product/upsert?id=${data}" class="btn btn-primary mx-2">
                            <i class="bi bi-pencil-square"></i> Edit
                        </a>
                        <a onClick=Delete('/admin/product/delete/${data}') class="btn btn-danger mx-2">
                            <i class="bi bi-trash-fill"></i> Delete
                        </a>
                    </div>`
                }
            }
        ],
        "drawCallback": function() {
            $('[data-bs-toggle="tooltip"]').tooltip();
        },
        "dom": '<"row"<"col-sm-6"l><"col-sm-6"f>>' +
               '<"row"<"col-sm-12"tr>>' +
               '<"row"<"col-sm-5"i><"col-sm-7"p>>',
        "ordering": true,
        "order": [[0, "asc"]],
        "pagingType": "full_numbers",
        "lengthMenu": [[10, 25, 50, -1], [10, 25, 50, "All"]],
        "responsive": true,
        "autoWidth": false,
        "stateSave": true
    });
}

function formatPriceVND(price) {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(price);
}

function Delete(url) {
    Swal.fire({
        title: 'Are you sure?',
        text: "You won't be able to revert this!",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Yes, delete it!'
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: url,
                type: 'DELETE',
                success: function (data) {
                    dataTable.ajax.reload();
                    toastr.success(data.message);
                }
            })
        }
    })
}