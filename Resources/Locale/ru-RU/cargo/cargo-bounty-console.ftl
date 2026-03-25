bounty-console-menu-title = Cargo bounty console
bounty-console-label-button-text = Печать этикетки
bounty-console-skip-button-text = Skip
bounty-console-time-label = Время: [color=orange]{ $time }[/color]
bounty-console-reward-label = Награда: [color=limegreen]${ $reward }[/color]
bounty-console-manifest-label = Манифест: [color=gray]{ $item }[/color]
bounty-console-manifest-entry =
    { $amount ->
        [1] { $item }
       *[other] { $item } x{ $amount }
    }
bounty-console-manifest-reward = Reward: ${ $reward }
bounty-console-description-label = [color=gray]{ $description }[/color]
bounty-console-id-label = ID#{ $id }
bounty-console-flavor-left = Награды от местных недобросовестных дилеров.
bounty-console-flavor-right = v1.4
bounty-manifest-header = Официальный грузовой манифест (ID#{ $id })
bounty-manifest-list-start = Манифест предмета:
bounty-console-tab-available-label = Available
bounty-console-tab-history-label = History
bounty-console-history-empty-label = No bounty history found
bounty-console-history-notice-completed-label = [color=limegreen]Completed[/color]
bounty-console-history-notice-skipped-label = [color=red]Skipped[/color] by { $id }
