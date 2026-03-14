/* ============================================================
   MediTech – Citas Calendar JavaScript
   FullCalendar init, event handling, timeline, search
   ============================================================ */

document.addEventListener('DOMContentLoaded', function () {

    // ── FullCalendar Init ──
    const calendarEl = document.getElementById('fullcalendar');
    if (!calendarEl) return;

    const calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'timeGridWeek',
        locale: 'es',
        headerToolbar: false,  // We use custom header
        nowIndicator: true,
        allDaySlot: false,
        slotMinTime: '06:00:00',
        slotMaxTime: '21:00:00',
        slotDuration: '00:30:00',
        slotLabelInterval: '01:00:00',
        height: '100%',
        expandRows: true,
        dayHeaderFormat: { weekday: 'short', day: 'numeric' },
        eventTimeFormat: {
            hour: 'numeric',
            minute: '2-digit',
            meridiem: 'short'
        },
        events: {
            url: '/Citas/GetEvents',
            method: 'GET',
            failure: function () {
                console.error('Error loading calendar events.');
            }
        },
        eventClick: function (info) {
            info.jsEvent.preventDefault();
            showEventPopover(info.event, info.jsEvent);
        },
        datesSet: function (dateInfo) {
            updateCalendarTitle(dateInfo);
        },
        eventDidMount: function (info) {
            // Add tooltip
            info.el.title = info.event.title + ' - ' +
                (info.event.extendedProps.tratamiento || '');
        }
    });

    calendar.render();
    window.mediCalendar = calendar;

    // ── Custom Header Controls ──
    const titleEl = document.getElementById('calendarTitle');
    const btnToday = document.getElementById('btnToday');
    const btnPrev = document.getElementById('btnPrev');
    const btnNext = document.getElementById('btnNext');

    btnToday?.addEventListener('click', () => {
        calendar.today();
    });
    btnPrev?.addEventListener('click', () => {
        calendar.prev();
    });
    btnNext?.addEventListener('click', () => {
        calendar.next();
    });

    // View switcher
    document.querySelectorAll('.view-switcher button').forEach(btn => {
        btn.addEventListener('click', function () {
            const view = this.dataset.view;
            if (!view) return;
            calendar.changeView(view);
            // Update active button
            document.querySelectorAll('.view-switcher button').forEach(b => b.classList.remove('active'));
            this.classList.add('active');
        });
    });

    function updateCalendarTitle(dateInfo) {
        if (!titleEl) return;
        const date = dateInfo.view.currentStart;
        const months = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
            'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
        titleEl.textContent = months[date.getMonth()] + ' ' + date.getFullYear();
    }

    // ── Event Popover ──
    const popoverEl = document.getElementById('eventPopover');
    let popoverTimeout;

    function showEventPopover(event, jsEvent) {
        if (!popoverEl) return;

        // Fetch event details
        fetch(`/Citas/GetEventDetail/${event.id}`)
            .then(r => r.json())
            .then(data => {
                document.getElementById('popoverTitle').textContent = data.paciente;
                document.getElementById('popoverTreatment').textContent = data.tratamiento;
                document.getElementById('popoverColorBar').style.backgroundColor = data.color;
                document.getElementById('popoverTime').textContent = `${data.horaInicio} - ${data.horaFin}`;
                document.getElementById('popoverDate').textContent = data.fecha;
                document.getElementById('popoverPhone').textContent = data.telefono || 'Sin teléfono';
                document.getElementById('popoverObs').textContent = data.observaciones || 'Sin observaciones';

                // Action links
                document.getElementById('popoverBtnView').href = `/Citas/Details/${data.id}`;
                document.getElementById('popoverBtnEdit').href = `/Citas/Edit/${data.id}`;
                document.getElementById('popoverBtnPatient').href = `/Pacientes/Details/${data.pacienteId}`;

                // Handle Marcar Atendida button
                const formAtendida = document.getElementById('formMarcarAtendida');
                if (data.estadoId === 1) { // 1 = Programada
                    formAtendida.style.display = 'block';
                    formAtendida.action = `/Citas/MarcarAtendida/${data.id}`;
                    document.getElementById('popoverAtendidaId').value = data.id;
                } else {
                    formAtendida.style.display = 'none';
                }

                // Position popover near click
                const x = Math.min(jsEvent.clientX, window.innerWidth - 320);
                const y = Math.min(jsEvent.clientY, window.innerHeight - 300);
                popoverEl.style.left = x + 'px';
                popoverEl.style.top = y + 'px';
                popoverEl.classList.add('active');
            })
            .catch(err => console.error('Error loading event detail:', err));
    }

    // Close popover
    document.getElementById('popoverClose')?.addEventListener('click', () => {
        popoverEl?.classList.remove('active');
    });

    document.addEventListener('click', function (e) {
        if (popoverEl && !popoverEl.contains(e.target) && !e.target.closest('.fc-event')) {
            popoverEl.classList.remove('active');
        }
    });

    // ── Today's Agenda Panel ──
    loadTodayAgenda();

    function loadTodayAgenda() {
        const agendaBody = document.getElementById('agendaPanelBody');
        const agendaCount = document.getElementById('agendaCount');
        const agendaDate = document.getElementById('agendaDate');
        if (!agendaBody) return;

        // Set today's date
        const today = new Date();
        const days = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
        const months = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
            'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
        if (agendaDate) {
            agendaDate.textContent = `${days[today.getDay()]}, ${today.getDate()} de ${months[today.getMonth()]}`;
        }

        fetch('/Citas/GetTodayAgenda')
            .then(r => r.json())
            .then(data => {
                if (agendaCount) agendaCount.textContent = data.count + ' citas';

                if (data.count === 0) {
                    agendaBody.innerHTML = `
                        <div class="agenda-empty">
                            <i class="fas fa-calendar-check"></i>
                            <p>No hay citas programadas para hoy</p>
                        </div>
                    `;
                    return;
                }

                let html = '';
                data.items.forEach(item => {
                    html += `
                        <div class="timeline-item">
                            <div class="timeline-dot" style="color: ${item.color};"></div>
                            <div class="timeline-card" style="border-left-color: ${item.color};" onclick="window.location.href='/Citas/Details/${item.id}'">
                                <div class="time-range" style="color: ${item.color};">
                                    <span class="check-icon" style="background: ${item.color};"><i class="fas fa-check"></i></span>
                                    ${item.horaInicio} - ${item.horaFin}
                                </div>
                                <div class="patient-name">${item.paciente}</div>
                                <div class="treatment-name">${item.tratamiento}${item.observaciones ? ' - ' + item.observaciones : ''}</div>
                                <div class="card-actions">
                                    <a href="/Citas/Details/${item.id}" class="action-btn" title="Ver Detalles" onclick="event.stopPropagation();">
                                        <i class="fas fa-file-alt"></i>
                                    </a>
                                    <a href="/Citas/Edit/${item.id}" class="action-btn" title="Editar" onclick="event.stopPropagation();">
                                        <i class="fas fa-edit"></i>
                                    </a>
                                    <a href="/Pacientes/Details/${item.pacienteId}" class="action-btn" title="Expediente" onclick="event.stopPropagation();">
                                        <i class="fas fa-clock"></i>
                                    </a>
                                </div>
                            </div>
                        </div>
                    `;
                });

                agendaBody.innerHTML = html;
            })
            .catch(err => {
                console.error('Error loading today agenda:', err);
                agendaBody.innerHTML = '<div class="agenda-empty"><p>Error al cargar la agenda</p></div>';
            });
    }

    // ── Search ──
    const searchInput = document.getElementById('calendarSearchInput');
    const searchDropdown = document.getElementById('searchResultsDropdown');
    let searchTimeout;

    searchInput?.addEventListener('input', function () {
        clearTimeout(searchTimeout);
        const term = this.value.trim();

        if (term.length < 2) {
            searchDropdown?.classList.remove('active');
            return;
        }

        searchTimeout = setTimeout(() => {
            fetch(`/Citas/Search?term=${encodeURIComponent(term)}`)
                .then(r => r.json())
                .then(data => {
                    let html = '';

                    if (data.patients.length > 0) {
                        html += '<div class="search-section-label">Pacientes</div>';
                        data.patients.forEach(p => {
                            html += `
                                <a href="/Pacientes/Details/${p.id}" class="search-result-item">
                                    <div class="result-icon patient"><i class="fas fa-user"></i></div>
                                    <div class="result-info">
                                        <div class="result-name">${p.nombre}</div>
                                        <div class="result-meta">${p.identificacion}</div>
                                    </div>
                                </a>
                            `;
                        });
                    }

                    if (data.appointments.length > 0) {
                        html += '<div class="search-section-label">Citas</div>';
                        data.appointments.forEach(a => {
                            html += `
                                <a href="/Citas/Details/${a.id}" class="search-result-item">
                                    <div class="result-icon appointment"><i class="fas fa-calendar-alt"></i></div>
                                    <div class="result-info">
                                        <div class="result-name">${a.paciente}</div>
                                        <div class="result-meta">${a.fecha} ${a.hora} · ${a.tratamiento}</div>
                                    </div>
                                </a>
                            `;
                        });
                    }

                    if (data.patients.length === 0 && data.appointments.length === 0) {
                        html = '<div class="search-no-results"><i class="fas fa-search"></i> Sin resultados</div>';
                    }

                    searchDropdown.innerHTML = html;
                    searchDropdown.classList.add('active');
                })
                .catch(err => console.error('Search error:', err));
        }, 300);
    });

    // Close search dropdown on outside click
    document.addEventListener('click', function (e) {
        if (searchDropdown && !searchDropdown.contains(e.target) && e.target !== searchInput) {
            searchDropdown.classList.remove('active');
        }
    });

    // ── Create Modal ──
    const modalBackdrop = document.getElementById('createModalBackdrop');
    const modalEl = document.getElementById('createModal');
    const btnOpenCreate = document.getElementById('btnCreateCita');
    const btnCloseModal = document.getElementById('btnCloseModal');

    btnOpenCreate?.addEventListener('click', () => openCreateModal());
    btnCloseModal?.addEventListener('click', () => closeCreateModal());
    modalBackdrop?.addEventListener('click', () => closeCreateModal());

    function openCreateModal() {
        modalBackdrop?.classList.add('active');
        modalEl?.classList.add('active');
        // Reset form
        const form = document.getElementById('createCitaForm');
        if (form) form.reset();
        const alert = document.getElementById('modalAlert');
        if (alert) {
            alert.className = 'modal-alert';
            alert.textContent = '';
        }
        // Set default date to today
        const dateInput = form?.querySelector('[name="Fecha"]');
        if (dateInput) {
            const today = new Date().toISOString().split('T')[0];
            dateInput.value = today;
            dateInput.min = today;
        }
    }

    function closeCreateModal() {
        modalBackdrop?.classList.remove('active');
        modalEl?.classList.remove('active');
        document.getElementById('pacienteSearchResults').style.display = 'none';
        document.getElementById('modalPacienteSearch').value = '';
        document.getElementById('modalPaciente').value = '';
    }

    // Modal Patient Autocomplete
    const mPacienteSearch = document.getElementById('modalPacienteSearch');
    const mPacienteHidden = document.getElementById('modalPaciente');
    const mSearchResults = document.getElementById('pacienteSearchResults');
    let mSearchTimeout;

    mPacienteSearch?.addEventListener('input', function() {
        clearTimeout(mSearchTimeout);
        mPacienteHidden.value = ''; // clear hidden value if they type
        const term = this.value.trim();

        if (term.length < 2) {
            mSearchResults.style.display = 'none';
            return;
        }

        mSearchTimeout = setTimeout(() => {
            fetch(`/Citas/BuscarPacientes?term=${encodeURIComponent(term)}`)
                .then(r => r.json())
                .then(data => {
                    let html = '';
                    if (data.length > 0) {
                        data.forEach(p => {
                            html += `
                                <a href="javascript:void(0)" class="list-group-item list-group-item-action py-2" data-id="${p.id}" data-name="${p.label}">
                                    <i class="fas fa-user-circle me-2 text-secondary"></i> ${p.label}
                                </a>
                            `;
                        });
                    } else {
                        html = '<div class="list-group-item text-muted"><i class="fas fa-search"></i> No se encontraron pacientes</div>';
                    }
                    mSearchResults.innerHTML = html;
                    mSearchResults.style.display = 'block';

                    // Attach click events
                    mSearchResults.querySelectorAll('a').forEach(item => {
                        item.addEventListener('click', function() {
                            mPacienteHidden.value = this.dataset.id;
                            mPacienteSearch.value = this.dataset.name;
                            mSearchResults.style.display = 'none';
                        });
                    });
                })
                .catch(err => console.error('Patient search error:', err));
        }, 300);
    });

    document.addEventListener('click', function (e) {
        if (mSearchResults && !mSearchResults.contains(e.target) && e.target !== mPacienteSearch) {
            mSearchResults.style.display = 'none';
        }
    });

    // Handle modal form submission
    const createForm = document.getElementById('createCitaForm');
    createForm?.addEventListener('submit', function (e) {
        e.preventDefault();
        const alertEl = document.getElementById('modalAlert');
        const submitBtn = createForm.querySelector('button[type="submit"]');

        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando...';

        const formData = new FormData(createForm);

        fetch('/Citas/CreateJson', {
            method: 'POST',
            body: formData
        })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    alertEl.className = 'modal-alert success';
                    alertEl.textContent = '¡Cita creada correctamente!';
                    // Refresh calendar and timeline
                    calendar.refetchEvents();
                    loadTodayAgenda();
                    setTimeout(() => closeCreateModal(), 800);
                } else {
                    alertEl.className = 'modal-alert error';
                    alertEl.textContent = data.message || 'Error al crear la cita.';
                }
            })
            .catch(() => {
                alertEl.className = 'modal-alert error';
                alertEl.textContent = 'Error de conexión.';
            })
            .finally(() => {
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-save"></i> Guardar Cita';
            });
    });

    // ── Keyboard shortcuts ──
    document.addEventListener('keydown', function (e) {
        // Escape closes popover and modal
        if (e.key === 'Escape') {
            popoverEl?.classList.remove('active');
            closeCreateModal();
        }
    });

});
