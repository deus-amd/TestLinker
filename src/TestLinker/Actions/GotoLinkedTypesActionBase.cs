// Copyright 2016, 2015, 2014 Matthias Koch
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.Actions;
using JetBrains.ReSharper.Feature.Services.Navigation.ExecutionHosting;
using JetBrains.ReSharper.Feature.Services.Occurrences;
using JetBrains.ReSharper.Feature.Services.Tree;
using JetBrains.ReSharper.Psi;
using JetBrains.TextControl;
using JetBrains.TextControl.DataContext;
using JetBrains.UI.ActionsRevised;
using JetBrains.UI.DataContext;
using JetBrains.UI.PopupWindowManager;
using JetBrains.Util;
using TestLinker.Navigation;
using TestLinker.Utils;

namespace TestLinker.Actions
{
  public abstract class GotoLinkedTypesActionBase : IActionWithExecuteRequirement, IExecutableAction
  {
    //private GroupingEvent _executionGroupingEvent;
    private ITypesFromTextControlService _typesFromTextControlService;
    private ISolution _solution;
    private ITextControl _textControl;
    private LinkedTypesService _linkedTypesService;
    private PopupWindowContextSource _popupWindowContextSource;

    protected abstract ISet<ITypeElement> GetLinkedTypes (LinkedTypesService linkedTypesService, List<ITypeElement> typesInContext);

    public IActionRequirement GetRequirement (IDataContext dataContext)
    {
      //_executionGroupingEvent = _executionGroupingEvent ?? CreateExecuteGroupingEvent(dataContext);
      return CurrentPsiFileRequirement.FromDataContext(dataContext);
    }

    public bool Update (IDataContext context, ActionPresentation presentation, [NotNull] DelegateUpdate nextUpdate)
    {
      var solution = context.GetData(ProjectModelDataConstants.SOLUTION);
      return solution != null;
    }

    public void Execute (IDataContext context, [NotNull] DelegateExecute nextExecute)
    {
      _typesFromTextControlService = context.GetComponent<ITypesFromTextControlService>().NotNull();
      _solution = context.GetData(ProjectModelDataConstants.SOLUTION).NotNull();
      _textControl = context.GetData(TextControlDataConstants.TEXT_CONTROL).NotNull();
      _linkedTypesService = _solution.GetComponent<LinkedTypesService>();
      _popupWindowContextSource = context.GetData(UIDataConstants.PopupWindowContextSource);

      //_executionGroupingEvent.FireIncoming();
      ExecuteProlongated();
    }

    private void ExecuteProlongated ()
    {
      var typesInContext = _typesFromTextControlService.GetTypesFromCaretOrFile(_textControl, _solution).ToList();
      var linkedTypes = GetLinkedTypes(_linkedTypesService, typesInContext);
      var occurrences = linkedTypes.Select(x => new LinkedTypesOccurrence(x, OccurrenceType.Occurrence)).ToList<IOccurrence>();

      if (occurrences.Count == 0)
        TryCreateProductionOrTestClass(typesInContext);
      else if (occurrences.Count == 1)
        NavigateToSingleOccurrence(occurrences.Single());
      else
        ShowOccurrencePopupMenu(typesInContext, occurrences);
    }

    private void TryCreateProductionOrTestClass (IReadOnlyCollection<ITypeElement> typesInContext)
    {
      if (typesInContext.Count != 1)
      {
        MessageBox.ShowInfo("There is no single class in context to create production/test class from.");
        return;
      }

      ModificationUtility.TryCreateTestOrProductionClass(typesInContext.Single(), _textControl);
    }

    private void NavigateToSingleOccurrence (IOccurrence occurence)
    {
      occurence.Navigate(_solution, _popupWindowContextSource, transferFocus: true);
    }

    private void ShowOccurrencePopupMenu (ICollection<ITypeElement> typesInContext, ICollection<IOccurrence> occurrences)
    {
      Func<OccurrenceBrowserDescriptor> descriptorBuilder = () => new LinkedTypesOccurrenceBrowserDescriptor(_solution, typesInContext, occurrences);
      var navigationExecutionHost = _solution.GetComponent<DefaultNavigationExecutionHost>();
      navigationExecutionHost.ShowGlobalPopupMenu(
          _solution,
          occurrences,
          activate: true,
          windowContext: _popupWindowContextSource,
          descriptorBuilder: descriptorBuilder,
          options: new OccurrencePresentationOptions(),
          skipMenuIfSingleEnabled: true,
          title: "Go to linked types ");
    }

    //private GroupingEvent CreateExecuteGroupingEvent (IDataContext dataContext)
    //{
    //  var shellLocks = dataContext.GetComponent<IShellLocks>();
    //  return shellLocks.GroupingEvents.CreateEvent(
    //      Lifetimes.Define().Lifetime,
    //      "GotoLinkedTypes",
    //      TimeSpan.FromMilliseconds(value: 1000),
    //      Rgc.Guarded,
    //      ExecuteProlongated);
    //}
  }
}