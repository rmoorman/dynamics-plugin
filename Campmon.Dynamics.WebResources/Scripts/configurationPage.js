﻿/// <reference path="Libraries/webApiRest.js" />
/// <reference path="Libraries/knockout340.js" />
(function (global, webAPI, ko) {
    'use strict';

    global.Campmon = global.Campmon || {};
    global.Campmon.ConfigurationPage = global.Campmon.ConfigurationPage || (function () {
        function CampmonViewModel() {
            var self = this;
            self.isLoading = ko.observable(true);

            self.clients = ko.observableArray();
            self.clientLists = ko.observableArray();

            self.selectedClient = ko.observable();
            self.selectedList = ko.observable();
            self.hasConnectionError = ko.observable(false);

            self.selectedPrimaryEmail = ko.observable();
        }

        function init() {
            var vm = new CampmonViewModel();

            vm.selectedClient.subscribe(function (selectedClient) {
                Campmon.Plugin.executeAction('getclientlist', selectedClient)
                    .then(function (result) {
                        //TODO: If no client lists default to Sync to New List Option
                        vm.clientLists(JSON.parse(result.body.OutputData));
                    }, function (error) {
                        alert("Error retrieving lists for selected client.");
                    });
            });

            ko.applyBindings(vm);

            Campmon.Plugin.executeAction('loadmetadata', null)
                .then(function (result) {
                    var config = JSON.parse(result.body.OutputData);
                    if (config.Error) {
                        alert(config.Error);
                    }
                    vm.clients(config.Clients);

                    if (vm.clients().length == 1) {
                        vm.selectedClient(vm.clients()[0]);
                    }
                    //todo: set view model props from config

                    vm.isLoading(false);
                }, function (error) {
                    vm.hasConnectionError = true;
                    console.log(JSON.parse(error.response.text));
                });
        }

        return {
            init: init
        };
    })();

    global.Campmon.Plugin = global.Campmon.Plugin || (function () {
        var _actionName = 'campmon_ExecuteOperationAction';

        var pluginInput = function (operation, data) {
            return {
                OperationName: operation,
                InputData: data
            };
        }

        var executeAction = function (action, data) {
            return webAPI.REST.executeUnboundAction(_actionName, pluginInput(action, data), null);
        }

        return {
            executeAction: executeAction
        }
    })();

})(this, webAPI, ko);
