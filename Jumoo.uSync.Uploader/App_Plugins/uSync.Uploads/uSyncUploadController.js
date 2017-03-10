angular.module('umbraco').controller('uSyncUploadController',
    ['$scope', '$http', 'notificationsService', 'Upload',
    function ($scope, $http, notificationsService, Upload) {

        var serviceRoot = 'backoffice/uSync/UploadApi/'
        $scope.changes = [];
        $scope.processing = false; 

        $scope.disabled = false;
        var button = angular.element('file');

        $scope.saveFiles = function () {
            $scope.disabled = true;

            Upload.upload({
                url: serviceRoot + "upload?name=" + $scope.name,
                data: { file: $scope.file }
            })
            .then(function (response) {
                $scope.disabled = false;
                notificationsService.success("Uploaded", response.data)
                $scope.getUploads();

            },
            function (repsonse) {
                $scope.disabled = false;
                notificationsService.success("Error", "there was an error" + response);
            });
        }

        $scope.getUploads = function () {

            $http.get(serviceRoot + "GetUploads")
                .then(function (response) {
                    $scope.uploads = response.data;
                });
        };

        $scope.process = function (upload) {
            $scope.disabled = true;
            $scope.processing = true;
            $http.get(serviceRoot + "Process?name=" + upload)
            .then(function (response) {
                $scope.changes = response.data;
                $scope.disabled = false;
                $scope.processing = false;
            });
        };

        $scope.delete = function (upload) {
            $http.get(serviceRoot + "Delete?name=" + upload)
            .then(function (response) {
                console.log('deleted');
                $scope.getUploads();
            });
        }


        $scope.getUploads();
    }]);