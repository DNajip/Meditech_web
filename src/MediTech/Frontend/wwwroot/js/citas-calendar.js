document.addEventListener('DOMContentLoaded', function () {

    // 1. DOM Elements References
    const calendarEl = document.getElementById('fullcalendar');
    const titleEl = document.getElementById('calendarTitle');
    const btnToday = document.getElementById('btnToday');
    const btnPrev = document.getElementById('btnPrevMonth');
    const btnNext = document.getElementById('btnNextMonth');
    const btnViewMonth = document.getElementById('btnViewMonth');
    const btnViewWeek = document.getElementById('btnViewWeek');
    const btnViewDay = document.getElementById('btnViewDay');
    
    const agendaBody = document.getElementById('agendaHoyList');
    const modalCrearEl = document.getElementById('modalCrearCita');
    const modalDetalleEl = document.getElementById('modalDetalleCita');

    const phoneInput = document.getElementById('modalTelefono');
    const phoneAlert = document.getElementById('phoneMatchAlert');
    let phoneTimeout = null;
    let lastMatchData = null;

    if (!calendarEl) return;

    // 2. Helper Functions
    function updateCalendarTitle(view) {
        if (!titleEl || !view) return;
        const date = view.currentStart;
        const months = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
            'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
        titleEl.textContent = months[date.getMonth()] + ' ' + date.getFullYear();
    }

    function formatAmPm(dateStr) {
        if (!dateStr) return '';
        const [h, m] = dateStr.split(':');
        let hours = parseInt(h);
        const ampm = hours >= 12 ? 'p.m.' : 'a.m.';
        hours = hours % 12;
        hours = hours ? hours : 12;
        return `${String(hours).padStart(2,'0')}:${m} ${ampm}`;
    }

    if (typeof FullCalendar === 'undefined') {
        console.error('FullCalendar is not defined. Check if the script is loaded correctly.');
        return;
    }

    // 3. FullCalendar Instance Configuration
    const calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'timeGridWeek',
        locale: 'es',
        headerToolbar: false,
        nowIndicator: true,
        allDaySlot: false,
        slotMinTime: '06:00:00',
        slotDuration: '00:30:00',
        slotLabelInterval: '01:00:00',
        height: '100%',
        expandRows: true,
        selectable: true,
        dayHeaderFormat: { weekday: 'short', day: 'numeric' },
        eventTimeFormat: {
            hour: 'numeric',
            minute: '2-digit',
            meridiem: 'short'
        },
        events: '/Citas/GetEvents',
        
        // CUSTOM EVENT RENDER
        eventContent: function(arg) {
            let treatmentText = arg.event.extendedProps.tratamiento || 'General';
            let timeText = arg.timeText;
            let isCanceled = arg.event.extendedProps.estadoId === 4;
            let canceledClass = isCanceled ? 'event-canceled' : '';
            
            let htmlStr = `
                <div class="custom-event-content ${canceledClass}">
                    <div class="custom-event-time">${timeText}</div>
                    <div class="custom-event-title">${arg.event.title}</div>
                    <div class="custom-event-treatment">${treatmentText}</div>
                </div>
            `;
            return { html: htmlStr };
        },

        select: function(info) {
            if (window.openCreateModal) window.openCreateModal(info.start);
        },
        eventClick: function (info) {
            info.jsEvent.preventDefault();
            if (window.showEventModal) window.showEventModal(info.event);
        },
        datesSet: function (dateInfo) {
            updateCalendarTitle(dateInfo.view);
        }
    });

    // 4. Custom Header & Navigation Events
    btnToday?.addEventListener('click', () => calendar.today());
    btnPrev?.addEventListener('click', () => calendar.prev());
    btnNext?.addEventListener('click', () => calendar.next());

    const viewButtons = [btnViewMonth, btnViewWeek, btnViewDay];
    
    btnViewMonth?.addEventListener('click', function() {
        calendar.changeView('dayGridMonth');
        updateActiveButton(this);
    });
    btnViewWeek?.addEventListener('click', function() {
        calendar.changeView('timeGridWeek');
        updateActiveButton(this);
    });
    btnViewDay?.addEventListener('click', function() {
        calendar.changeView('timeGridDay');
        updateActiveButton(this);
    });

    function updateActiveButton(activeBtn) {
        viewButtons.forEach(btn => {
            if(!btn) return;
            btn.classList.remove('active');
            btn.style.backgroundColor = '#FFF';
            btn.style.color = '#6B7280';
            btn.classList.add('bg-white', 'text-secondary');
        });
        activeBtn.classList.remove('bg-white', 'text-secondary');
        activeBtn.classList.add('active');
        activeBtn.style.backgroundColor = '#4F46E5';
        activeBtn.style.color = 'white';
    }

    // 5. Offcanvas Agenda Logic
    window.loadTodayAgenda = function() {
        if (!agendaBody) return;
        agendaBody.innerHTML = '<div class="text-center p-4"><div class="spinner-border text-primary" role="status"></div></div>';
        
        fetch('/Citas/Hoy')
            .then(r => r.json())
            .then(data => {
                if (!data || data.length === 0) {
                    agendaBody.innerHTML = `
                        <div class="text-center text-muted p-5 mt-4">
                            <i class="bi bi-calendar-x fs-1 text-secondary opacity-50 mb-3 block"></i>
                            <p class="fw-semibold">No hay citas programadas para hoy</p>
                            <button class="btn btn-sm btn-outline-primary mt-2" data-bs-toggle="modal" data-bs-target="#modalCrearCita">Agendar una</button>
                        </div>`;
                    return;
                }
                
                let html = '';
                data.forEach(c => {
                    // status check: icon and color manipulation
                    const isAttended = c.estadoId === 2;
                    let statusIcon = isAttended ? '<i class="bi bi-check-circle-fill status-check"></i>' : '<i class="bi bi-circle status-pending"></i>';

                    html += `
                        <div class="today-card" onclick="openDetailsFromAgenda(${c.id})">
                            <div class="today-card-info w-100 pe-3">
                                <div class="today-card-time">
                                    <i class="bi bi-clock"></i> ${formatAmPm(c.horaInicio)} - ${formatAmPm(c.horaFin)}
                                </div>
                                <div class="today-card-name">${c.paciente}</div>
                                <div class="today-card-phone">
                                    <i class="bi bi-telephone"></i> ${c.telefono || 'Sin registro'}
                                </div>
                                <div class="today-card-treatment">
                                    <i class="bi bi-person-badge me-1"></i> ${c.tratamiento}
                                </div>
                            </div>
                            <div class="today-card-status">
                                ${statusIcon}
                            </div>
                        </div>`;
                });
                agendaBody.innerHTML = html;
            })
            .catch(err => {
                console.error('Error agenda:', err);
                agendaBody.innerHTML = '<div class="alert alert-danger mx-3 mt-3">Error al cargar la agenda de hoy</div>';
            });
    };

    window.openDetailsFromAgenda = function(id) {
        // We simulate finding the event in fullcalendar to launch the modal
        const evt = calendar.getEventById(id);
        if (evt) {
            window.showEventModal(evt);
        } else {
            // Event is somehow not in current calendar view, fallback manually
            fetch(`/Citas/GetEventDetail/${id}`)
                .then(r => r.json())
                .then(data => showEventModalWithData(data));
        }
    };


    // 6. Create Modal Functions (Simplified Redesign)
    window.openCreateModal = function(startTime) {
        if (!modalCrearEl) return;
        const form = document.getElementById('formCita');
        if (form) form.reset();
        
        const dateInput = document.getElementById('modalFecha');
        const start = (startTime && !isNaN(new Date(startTime))) ? new Date(startTime) : new Date();
        
        if (dateInput) {
            const year = start.getFullYear();
            const month = String(start.getMonth() + 1).padStart(2, '0');
            const day = String(start.getDate()).padStart(2, '0');
            dateInput.value = `${year}-${month}-${day}`;
        }
        
        document.getElementById('HoraInicio').value = start.toTimeString().slice(0,5);
        const end = new Date(start.getTime() + 30 * 60000);
        document.getElementById('HoraFin').value = end.toTimeString().slice(0,5);

        // UI Reset
        document.getElementById('modalAlert').classList.add('d-none');
        document.getElementById('tipoPacienteExistente').checked = true;
        window.toggleFields('paciente');
        
        // Clear Search Results
        document.getElementById('pacienteSearchResults').style.display = 'none';
        document.getElementById('tratamientoSearchResults').style.display = 'none';
        document.getElementById('modalTratamientoId').value = "";
        document.getElementById('modalTratamientoSearch').value = "";
        document.getElementById('PacienteId').value = "";
        document.getElementById('PosiblePacienteId').value = "";

        // Reset Phone Alert
        if (phoneAlert) phoneAlert.classList.add('d-none');
        lastMatchData = null;

        const modal = new bootstrap.Modal(modalCrearEl);
        modal.show();
    };

    // Phone Lookup Logic
    phoneInput?.addEventListener('input', function() {
        clearTimeout(phoneTimeout);
        if (phoneAlert) phoneAlert.classList.add('d-none');
        
        const phone = this.value.trim();
        if (phone.length < 4) return;

        phoneTimeout = setTimeout(() => {
            fetch(`/Citas/BuscarPacientePorTelefono?telefono=${encodeURIComponent(phone)}`)
                .then(r => r.json())
                .then(data => {
                    if (data.success) {
                        lastMatchData = data;
                        if (phoneAlert) {
                            phoneAlert.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-1"></i> Teléfono registrado a nombre de <strong>${data.nombre}</strong>. <button type="button" class="btn btn-link p-0 fw-bold text-decoration-none" style="font-size: 0.75rem;" onclick="useExistingRecord()">Vincular perfil</button>`;
                            phoneAlert.classList.remove('alert-info');
                            phoneAlert.classList.add('alert-warning');
                            phoneAlert.classList.remove('d-none');
                        }
                        
                        const selectedType = document.querySelector('input[name="tipoPaciente"]:checked')?.value;
                        if (selectedType === 'prospecto') {
                             document.getElementById('btnGuardarCita').disabled = true;
                        }
                    } else {
                        lastMatchData = null;
                        document.getElementById('btnGuardarCita').disabled = false;
                        const selectedType = document.querySelector('input[name="tipoPaciente"]:checked')?.value;
                        if (selectedType === 'prospecto') {
                             document.getElementById('PosiblePacienteId').value = "";
                             document.getElementById('PacienteId').value = "";
                        }
                    }
                });
        }, 800);
    });

    window.useExistingRecord = function() {
        if (!lastMatchData) return;
        
        if (lastMatchData.isProspect) {
            document.getElementById('tipoNuevoProspecto').checked = true;
            toggleFields('prospecto');
            document.getElementById('modalProspectoNombre').value = lastMatchData.nombre.split(' ')[0] || "";
            document.getElementById('modalProspectoApellido').value = lastMatchData.nombre.split(' ')[1] || "";
            document.getElementById('PosiblePacienteId').value = lastMatchData.id;
            document.getElementById('PacienteId').value = "";
        } else {
            document.getElementById('tipoPacienteExistente').checked = true;
            toggleFields('paciente');
            document.getElementById('modalPacienteSearch').value = lastMatchData.nombre;
            document.getElementById('PacienteId').value = lastMatchData.id;
            document.getElementById('PosiblePacienteId').value = "";
        }
        phoneAlert.classList.add('d-none');
        document.getElementById('btnGuardarCita').disabled = false;
    };

    // Al cambiar de tipo de paciente, limpiamos el estado
    window.toggleFields = function(type) {
        document.getElementById('pacientePanel').style.display = type === 'paciente' ? 'block' : 'none';
        document.getElementById('prospectoPanel').style.display = type === 'prospecto' ? 'block' : 'none';
        
        // Reset IDs if switching
        if (type === 'paciente') {
            document.getElementById('modalProspectoNombre').value = "";
            document.getElementById('modalProspectoSegundoNombre').value = "";
            document.getElementById('modalProspectoApellido').value = "";
            document.getElementById('modalProspectoSegundoApellido').value = "";
            document.getElementById('PosiblePacienteId').value = "";
        } else {
            document.getElementById('modalPacienteSearch').value = "";
            document.getElementById('PacienteId').value = "";
        }
        
        // Si el teléfono actual tenía match, re-validar bloqueo
        if (lastMatchData) {
            if (type === 'prospecto' && !document.getElementById('PosiblePacienteId').value) {
                document.getElementById('btnGuardarCita').disabled = true;
                phoneAlert.classList.remove('d-none');
            } else {
                document.getElementById('btnGuardarCita').disabled = false;
            }
        }
    };

    // Patient Search Autocomplete
    const patientSearch = document.getElementById('modalPacienteSearch');
    const patientResults = document.getElementById('pacienteSearchResults');

    patientSearch?.addEventListener('input', function() {
        const term = this.value.trim();
        if (term.length < 2) { patientResults.style.display = 'none'; return; }
        
        fetch(`/Citas/BuscarPacientes?term=${encodeURIComponent(term)}`)
            .then(r => r.json())
            .then(data => {
                if (data.length === 0) {
                    patientResults.innerHTML = '<div class="list-group-item text-muted text-center py-2" style="font-size: 0.8rem;">No se encontraron resultados</div>';
                } else {
                    let html = '';
                    data.forEach(item => {
                        html += `<button type="button" class="list-group-item list-group-item-action py-2" style="font-size: 0.8rem;" onclick="seleccionarPaciente('${item.id}', '${item.label}', ${item.isProspect}, '${item.telefono || ''}')">${item.label}</button>`;
                    });
                    patientResults.innerHTML = html;
                }
                patientResults.style.display = 'block';
            });
    });

    window.seleccionarPaciente = function(id, label, isProspect, phone) {
        document.getElementById('modalPacienteSearch').value = label;
        if (phone) {
            document.getElementById('modalTelefono').value = phone;
        }
        if (isProspect) {
            document.getElementById('PacienteId').value = ""; 
            document.getElementById('PosiblePacienteId').value = id;
            document.getElementById('tipoNuevoProspecto').checked = true;
            toggleFields('prospecto');
        } else {
            document.getElementById('PacienteId').value = id;
            document.getElementById('PosiblePacienteId').value = "";
            document.getElementById('tipoPacienteExistente').checked = true;
            toggleFields('paciente');
        }
        document.getElementById('pacienteSearchResults').style.display = 'none';
        document.getElementById('modalAlert').classList.add('d-none');
    };

    // Treatment Autocomplete
    const treatmentSearch = document.getElementById('modalTratamientoSearch');
    const treatmentResults = document.getElementById('tratamientoSearchResults');
    const treatmentId = document.getElementById('modalTratamientoId');

    treatmentSearch?.addEventListener('input', function() {
        const term = this.value.trim();
        if (term.length < 1) { treatmentResults.style.display = 'none'; return; }
        
        fetch(`/Citas/BuscarTratamientos?term=${encodeURIComponent(term)}`)
            .then(r => r.json())
            .then(data => {
                if (data.length === 0) {
                    treatmentResults.innerHTML = '<div class="list-group-item text-muted text-center py-2" style="font-size: 0.8rem;">No hay sugerencias</div>';
                } else {
                    let html = '';
                    data.forEach(item => {
                        html += `<button type="button" class="list-group-item list-group-item-action py-2" style="font-size: 0.8rem;" onclick="seleccionarTratamiento('${item.id}', '${item.nombre}')">${item.nombre}</button>`;
                    });
                    treatmentResults.innerHTML = html;
                }
                treatmentResults.style.display = 'block';
            });
    });

    window.seleccionarTratamiento = function(id, nombre) {
        treatmentSearch.value = nombre;
        treatmentId.value = id;
        treatmentResults.style.display = 'none';
        document.getElementById('modalAlert').classList.add('d-none');
    };

    // Global click listener to close results
    document.addEventListener('click', function(e) {
        if (!e.target.closest('#pacientePanel')) patientResults.style.display = 'none';
        if (!e.target.closest('#tratamientoSearchResults') && e.target !== treatmentSearch) treatmentResults.style.display = 'none';
    });

    // Guardar Cita Logic
    window.guardarCita = function() {
        const type = document.querySelector('input[name="tipoPaciente"]:checked').value;
        const form = document.getElementById('formCita');
        const alertEl = document.getElementById('modalAlert');
        
        // Limpiar alertas previas
        alertEl.classList.add('d-none');
        alertEl.textContent = "";

        // Limpiar teléfono (solo dígitos)
        const rawPhone = phoneInput.value;
        const cleanPhone = rawPhone.replace(/\D/g, '');
        phoneInput.value = cleanPhone;

        // Validaciones de UI
        if (type === 'prospecto') {
            if (!document.getElementById('modalProspectoNombre').value || !document.getElementById('modalProspectoApellido').value) {
                alertEl.textContent = "Por favor complete nombre y apellido del prospecto.";
                alertEl.classList.remove('d-none');
                return;
            }
        } else {
            if (!document.getElementById('PacienteId').value && !document.getElementById('PosiblePacienteId').value) {
                alertEl.textContent = "Por favor seleccione un paciente de la lista de resultados.";
                alertEl.classList.remove('d-none');
                return;
            }
        }

        if (!treatmentId.value) {
            alertEl.textContent = "Debe seleccionar un tratamiento de la lista de sugerencias.";
            alertEl.classList.remove('d-none');
            return;
        }

        const btn = document.getElementById('btnGuardarCita');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Guardando...';

        // 1. If it's a NEW prospect (manual entry, no PosPId yet)
        if (type === 'prospecto' && !document.getElementById('PosiblePacienteId').value) {
            const prospectoData = {
                primerNombre: document.getElementById('modalProspectoNombre').value,
                segundoNombre: document.getElementById('modalProspectoSegundoNombre').value,
                primerApellido: document.getElementById('modalProspectoApellido').value,
                segundoApellido: document.getElementById('modalProspectoSegundoApellido').value,
                telefono: cleanPhone
            };
            
            // Build query params including all 4 names
            const params = new URLSearchParams(prospectoData);
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            
            fetch(`/Citas/CreateProspectoJson`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': token, 'Content-Type': 'application/x-www-form-urlencoded' },
                body: params.toString()
            })
                .then(r => r.json())
                .then(data => {
                    if (data.success) {
                        document.getElementById('PosiblePacienteId').value = data.id;
                        submitAppointment();
                    } else {
                        MediToast.error(data.message);
                        btn.disabled = false;
                        btn.innerHTML = 'Agendar Cita';
                    }
                });
        } else {
            submitAppointment();
        }
    };

    function submitAppointment() {
        const form = document.getElementById('formCita');
        const formData = new FormData(form);
        const btn = document.getElementById('btnGuardarCita');

        fetch('/Citas/CreateJson', { method: 'POST', body: formData })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    calendar.refetchEvents();
                    window.loadTodayAgenda();
                    bootstrap.Modal.getInstance(modalCrearEl).hide();

                    // Limpieza de estados de match
                    const phoneAlert = document.getElementById('phoneMatchAlert');
                    if (phoneAlert) phoneAlert.classList.add('d-none');
                    // Nota: lastMatchData se resetea al abrir el modal o al limpiar el form
                } else {
                    document.getElementById('modalAlert').classList.remove('d-none');
                    document.getElementById('modalAlert').textContent = data.message;
                }
            })
            .finally(() => {
                btn.disabled = false;
                btn.innerHTML = 'Agendar Cita';
            });
    }

    // 7. Modal Details Logic
    window.showEventModal = function(event) {
        fetch(`/Citas/GetEventDetail/${event.id}`)
            .then(r => r.json())
            .then(data => showEventModalWithData(data));
    };

    function showEventModalWithData(data) {
        if (!modalDetalleEl) return;

        document.getElementById('detailPacienteNombre').textContent = data.paciente;
        document.getElementById('detailPacienteId').textContent = data.identificacion ? `Cédula: ${data.identificacion}` : '';
        document.getElementById('detailHorario').textContent = `${data.horaInicio} - ${data.horaFin}`;
        document.getElementById('detailFecha').textContent = data.fecha;
        document.getElementById('detailTelefono').textContent = data.telefono || 'No especificado';
        document.getElementById('detailTratamiento').textContent = data.tratamiento;

        const obsRow = document.getElementById('detailObsRow');
        if(data.observaciones && data.observaciones.trim() !== '') {
            document.getElementById('detailObservaciones').textContent = data.observaciones;
            obsRow.style.display = 'flex';
        } else {
            obsRow.style.setProperty('display', 'none', 'important');
        }

        const badge = document.getElementById('detailProspectBadge');
        if(data.pacienteId === null) {
            badge.style.display = 'inline-block';
        } else {
            badge.style.display = 'none';
        }

        const btnGroupActions = document.getElementById('btnGroupActions');
        btnGroupActions.innerHTML = ''; 

        if (data.pacienteId === null) {
            btnGroupActions.innerHTML = `<button type="button" class="btn text-white w-100 fw-semibold py-2" style="background-color: #4F46E5; border-radius: 8px;" onclick="abrirModalConversionJS(${data.id}, ${data.posiblePacienteId})"><i class="bi bi-person-check me-1"></i> Convertir a Paciente</button>`;
        } else {
            if (data.estadoId === 1) { 
                btnGroupActions.innerHTML = `<button type="button" class="btn btn-success text-white w-100 fw-semibold py-2 shadow-sm" style="border-radius: 8px;" onclick="marcarAtendida(${data.id})"><i class="bi bi-check2-circle me-1"></i> Confirmar Llegada</button>
                                             <a href="/Pacientes/Ficha/${data.pacienteId}" class="btn text-white fw-semibold py-2" style="background-color: #4F46E5; border-radius: 8px;"><i class="bi bi-folder2-open"></i></a>`;
            } else if (data.estadoId === 2) { 
                btnGroupActions.innerHTML = `<button type="button" class="btn text-white w-100 fw-semibold py-2 shadow-sm" style="background-color: #4F46E5; border-radius: 8px;" onclick="iniciarConsultaJS(${data.id})"><i class="bi bi-stethoscope me-1"></i> Iniciar Consulta</button>`;
            } else if (data.estadoId === 3) {
                btnGroupActions.innerHTML = `<span class="badge bg-success w-100 p-3 fs-6 rounded-3"><i class="bi bi-check-circle me-1"></i> Cita Realizada</span>`;
            } else {
                btnGroupActions.innerHTML = `<span class="badge bg-danger w-100 p-3 fs-6 rounded-3">Cita Cancelada</span>`;
            }
        }

        const btnCancel = document.getElementById('btnCancelCita');
        if (data.estadoId === 4 || data.estadoId === 2 || data.estadoId === 3) {
            btnCancel.style.display = 'none';
        } else {
            btnCancel.style.display = 'block';
            btnCancel.onclick = function() {
                MediConfirm.show({
                    title: '¿Cancelar esta cita?',
                    message: 'Esta acción no se puede deshacer y la cita se marcará como cancelada.',
                    variant: 'danger',
                    confirmText: 'Sí, cancelar cita',
                    cancelText: 'No, mantener'
                }).then(confirmed => {
                    if (confirmed) {
                        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                        const formData = new FormData();
                        if (token) formData.append('__RequestVerificationToken', token);

                        fetch(`/Citas/Cancel/${data.id}`, {
                            method: 'POST',
                            body: formData
                        })
                        .then(r => r.json())
                        .then(res => {
                            if (res.success) {
                                MediToast.success("Cita cancelada correctamente.");
                                calendar.refetchEvents();
                                bootstrap.Modal.getInstance(modalDetalleEl).hide();
                                if (window.loadTodayAgenda) window.loadTodayAgenda();
                            } else {
                                MediToast.error(res.message || "Error al cancelar la cita.");
                            }
                        })
                        .catch(err => {
                            console.error('Error cancelando cita:', err);
                            MediToast.error("Error de conexión al intentar cancelar la cita.");
                        });
                    }
                });
            };
        }

        const bsModal = new bootstrap.Modal(modalDetalleEl);
        bsModal.show();
    }

    window.abrirModalConversionJS = function(idCita, idPosP) {
        bootstrap.Modal.getInstance(modalDetalleEl).hide();
        document.getElementById('convertIdCita').value = idCita;
        document.getElementById('convertIdPosiblePaciente').value = idPosP;
        const m = new bootstrap.Modal(document.getElementById('modalConvertir'));
        m.show();
    }

    window.iniciarConsultaJS = function(idCita) {
        const modal = bootstrap.Modal.getInstance(modalDetalleEl);
        if (modal) modal.hide();

        // Fetch triage data from the server
        fetch(`/Citas/GetRecepcionData/${idCita}`)
            .then(r => r.json())
            .then(data => {
                if (!data.success) {
                    MediToast.error(data.error || 'Error al cargar datos de recepción.');
                    return;
                }

                // Populate hidden fields
                document.getElementById('recIdCita').value = data.idCita;
                document.getElementById('recIdMedico').value = data.idMedico;
                document.getElementById('recIdEstado').value = data.idEstado;
                document.getElementById('recIdConsulta').value = data.idConsulta;

                // Populate header
                document.getElementById('recepcionPacienteNombre').textContent = data.pacienteNombre;
                document.getElementById('recepcionFechaHora').textContent = data.fechaHora;
                document.getElementById('recepcionTratamiento').textContent = data.tratamiento;

                // Populate signos vitales if existing
                document.getElementById('recPresion').value = data.signos?.presionArterial || '';
                document.getElementById('recTemp').value = data.signos?.temperatura || '';
                document.getElementById('recFC').value = data.signos?.frecuenciaCardiaca || '';
                document.getElementById('recSat').value = data.signos?.saturacionOxigeno || '';
                document.getElementById('recPeso').value = data.signos?.peso || '';
                document.getElementById('recAltura').value = data.signos?.altura || '';

                // Populate clinical data (motivo = tratamiento agendado)
                document.getElementById('recMotivo').value = data.motivo || data.tratamiento || '';
                document.getElementById('recObservaciones').value = data.observaciones || data.observacionesCita || '';

                // Reset alert
                document.getElementById('recepcionAlert').classList.add('d-none');

                // Calculate IMC if data exists
                calcularIMCRecepcion();

                // Open modal
                const recModal = new bootstrap.Modal(document.getElementById('modalRecepcion'));
                recModal.show();
            })
            .catch(err => {
                console.error('Error:', err);
                MediToast.error('Error de conexión al cargar el formulario de recepción.');
            });
    }

    // IMC auto-calculation for recepcion modal (peso en lbs)
    window.calcularIMCRecepcion = function() {
        const pesoLbs = parseFloat(document.getElementById('recPeso').value);
        const altura = parseFloat(document.getElementById('recAltura').value);
        const imcVal = document.getElementById('recImcValue');
        const imcStat = document.getElementById('recImcStatus');

        if (pesoLbs > 0 && altura > 0) {
            const pesoKg = pesoLbs / 2.205; // Convertir lbs a kg
            const alturaM = altura > 3 ? altura / 100 : altura;
            const imc = pesoKg / (alturaM * alturaM);
            imcVal.textContent = imc.toFixed(1);

            if (imc < 18.5) { imcStat.textContent = "Bajo peso"; imcStat.className = "fw-bold text-warning"; }
            else if (imc < 25) { imcStat.textContent = "Peso normal"; imcStat.className = "fw-bold text-success"; }
            else if (imc < 30) { imcStat.textContent = "Sobrepeso"; imcStat.className = "fw-bold text-warning"; }
            else { imcStat.textContent = "Obesidad"; imcStat.className = "fw-bold text-danger"; }
        } else {
            imcVal.textContent = "--.-";
            imcStat.textContent = "Introduzca peso y altura";
            imcStat.className = "fw-bold text-secondary";
        }
    }

    // Attach IMC listeners
    document.getElementById('recPeso')?.addEventListener('input', calcularIMCRecepcion);
    document.getElementById('recAltura')?.addEventListener('input', calcularIMCRecepcion);

    // Save recepcion via AJAX
    window.guardarRecepcion = function() {
        const form = document.getElementById('formRecepcion');
        const formData = new FormData(form);
        const btn = document.getElementById('btnGuardarRecepcion');
        const alertEl = document.getElementById('recepcionAlert');

        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Guardando...';
        alertEl.classList.add('d-none');

        fetch('/Citas/GuardarRecepcion', {
            method: 'POST',
            body: formData
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                window.location.href = `/Pacientes/Ficha/${data.idPaciente}?consultaId=${data.idConsulta}&modo=consulta`;
            } else {
                alertEl.textContent = data.error || 'Error al guardar la recepción.';
                alertEl.classList.remove('d-none');
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-check2-circle me-1"></i> Guardar y continuar';
            }
        })
        .catch(err => {
            console.error('Error:', err);
            alertEl.textContent = 'Error de conexión al guardar.';
            alertEl.classList.remove('d-none');
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check2-circle me-1"></i> Guardar y continuar';
        });
    }

    window.marcarAtendida = function(idCita) {
        MediConfirm.show({
            title: 'Confirmar llegada',
            message: '¿Desea marcar a este paciente como presente en la clínica?',
            variant: 'success',
            confirmText: 'Sí, confirmar llegada',
            cancelText: 'Cancelar'
        }).then(confirmed => {
            if (!confirmed) return;

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token);

            fetch(`/Citas/MarcarAtendida/${idCita}`, {
                method: 'POST',
                body: formData
            })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    MediToast.success("Llegada confirmada.");
                    calendar.refetchEvents();
                    bootstrap.Modal.getInstance(modalDetalleEl).hide();
                    if (window.loadTodayAgenda) window.loadTodayAgenda();
                } else {
                    MediToast.error(data.message || 'Error al confirmar llegada.');
                }
            })
            .catch(err => {
                console.error('Error:', err);
                MediToast.error('Error de conexión al confirmar llegada.');
            });
        });
    }

    window.ejecutarConversion = function() {
        const form = document.getElementById('formConvertir');
        if (!form.checkValidity()) { form.reportValidity(); return; }
        
        const btn = document.getElementById('btnConfirmConversion');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Procesando...';

        fetch('/Citas/ConvertirProspecto', {
            method: 'POST',
            body: new FormData(form)
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                document.getElementById('formConvertir').reset();
                calendar.refetchEvents();
                bootstrap.Modal.getInstance(document.getElementById('modalConvertir')).hide();
                
                if (typeof MediToast !== 'undefined') {
                    MediToast.success('Conversión finalizada');
                }
                
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-check-circle me-1"></i> Finalizar Conversión';
                
                if (window.loadTodayAgenda) window.loadTodayAgenda();
            } else {
                MediToast.error(data.message);
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-check-circle me-1"></i> Finalizar Conversión';
            }
        });
    };

    // 9. Initial Render & Load
    console.log('Rendering calendar...');
    calendar.render();
    window.loadTodayAgenda();
    
    // Hide loading spinner after render
    setTimeout(() => {
        const loadingEl = document.getElementById('fullcalendar-loading');
        if (loadingEl) loadingEl.style.display = 'none';
        if (calendarEl) calendarEl.style.opacity = '1';
    }, 500);

    // Sync initial view button state (default is week)
    if (btnViewWeek) updateActiveButton(btnViewWeek);
});
