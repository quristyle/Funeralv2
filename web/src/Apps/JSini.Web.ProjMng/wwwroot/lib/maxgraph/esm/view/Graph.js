/*
Copyright 2021-present The maxGraph project Contributors
Copyright (c) 2006-2015, JGraph Ltd
Copyright (c) 2006-2015, Gaudenz Alder

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
import { AbstractGraph } from './AbstractGraph.js';
import GraphView from './GraphView.js';
import CellRenderer from './cell/CellRenderer.js';
import GraphDataModel from './GraphDataModel.js';
import { Stylesheet } from './style/Stylesheet.js';
import GraphSelectionModel from './GraphSelectionModel.js';
import { registerDefaultShapes } from './shape/register-shapes.js';
import { registerDefaultEdgeMarkers, registerDefaultEdgeStyles, registerDefaultPerimeters, } from './style/register.js';
import { getDefaultPlugins } from './plugin/index.js';
/**
 * An implementation of {@link AbstractGraph} that automatically loads some default built-ins (plugins, style elements).
 *
 * Good for evaluation and prototyping, but not recommended for production use. Use {@link BaseGraph} instead.
 *
 * @category Graph
 */
export class Graph extends AbstractGraph {
    /**
     * Creates a new {@link CellRenderer} to be used in this graph.
     */
    createCellRenderer() {
        return new CellRenderer();
    }
    /**
     * Creates a new {@link GraphDataModel} to be used in this graph.
     */
    createGraphDataModel() {
        return new GraphDataModel();
    }
    /**
     * Creates a new {@link GraphView} to be used in this graph.
     */
    createGraphView() {
        return new GraphView(this);
    }
    /**
     * Creates a new {@link GraphSelectionModel} to be used in this graph.
     */
    createSelectionModel() {
        return new GraphSelectionModel(this);
    }
    /**
     * Creates a new {@link Stylesheet} to be used in this graph.
     */
    createStylesheet() {
        return new Stylesheet();
    }
    // Register all builtins provided by maxGraph
    registerDefaults() {
        registerDefaultEdgeMarkers();
        registerDefaultEdgeStyles();
        registerDefaultPerimeters();
        registerDefaultShapes();
    }
    // Build cellRenderer/selectionModel/view via the factory methods so subclasses can customize them
    // by overriding. Only `model` and `stylesheet` are accepted from options because Graph's positional
    // constructor doesn't expose the others.
    initializeCollaborators(options) {
        this.cellRenderer = this.createCellRenderer();
        this.model = options?.model ?? this.createGraphDataModel();
        this.setSelectionModel(this.createSelectionModel());
        this.setStylesheet(options?.stylesheet ?? this.createStylesheet());
        this.view = this.createGraphView();
    }
    constructor(container, model, plugins = getDefaultPlugins(), stylesheet) {
        super({ container, model, plugins, stylesheet: stylesheet ?? undefined });
    }
}
